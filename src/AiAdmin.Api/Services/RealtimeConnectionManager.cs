using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     管理实时 WebSocket 连接：连接建立时推送系统版本和未读站内信数量，并支持按用户推送未读数变化
/// </summary>
/// <param name="scopeFactory">服务作用域工厂，用于在单例中访问数据库</param>
/// <param name="systemInfo">系统信息服务</param>
/// <param name="logger">日志记录器</param>
/// <param name="bus">实时推送总线，多实例部署时负责把推送广播给连接在其他实例上的用户</param>
public sealed class RealtimeConnectionManager(
    IServiceScopeFactory scopeFactory
    , SystemInfoService systemInfo
    , ILogger<RealtimeConnectionManager> logger
    , IRealtimeBus bus)
{
    private const int _RECEIVE_BUFFER_SIZE = 256;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, long, Exception?> _logAborted = LoggerMessage.Define<long>(
        LogLevel.Debug, new EventId(3312, "RealtimeConnectionAborted"), "Realtime connection aborted for user {UserId}"
    );

    private static readonly Action<ILogger, long, Exception?> _logCanceled = LoggerMessage.Define<long>(
        LogLevel.Debug, new EventId(3311, "RealtimeConnectionCanceled"), "Realtime connection canceled for user {UserId}"
    );

    private static readonly Action<ILogger, long, Exception?> _logPushFailed = LoggerMessage.Define<long>(
        LogLevel.Debug, new EventId(3313, "RealtimePushFailed"), "Realtime push failed for user {UserId}"
    );

    private static readonly Action<ILogger, long, Exception?> _logPushSkipped = LoggerMessage.Define<long>(
        LogLevel.Debug, new EventId(3314, "RealtimePushSkipped"), "Realtime push skipped for closed connection of user {UserId}"
    );

    private static readonly Action<ILogger, long, Exception?> _logSupportPushFailed = LoggerMessage.Define<long>(
        LogLevel.Debug, new EventId(3315, "RealtimeSupportPushFailed"), "Realtime support push failed for user {UserId}"
    );

    private readonly ConcurrentDictionary<Guid, RealtimeConnection> _connections = new();
    private int _subscribed;

    /// <summary>
    ///     向所有在线连接发送关闭帧，安全停机时用于让长连接主动退出；前端收到后会按重连逻辑访问并被闸门拒绝
    /// </summary>
    /// <returns>异步关闭任务</returns>
    public async Task CloseAllAsync() {
        foreach (var connection in _connections.Values.ToList()) {
            try {
                if (connection.Socket.State == WebSocketState.Open) {
                    await connection
                        .Socket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "shutting down", CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch (WebSocketException exception) {
                _logAborted(logger, connection.UserId, exception);
            }
            catch (ObjectDisposedException exception) {
                _logPushSkipped(logger, connection.UserId, exception);
            }
        }
    }

    /// <summary>
    ///     处理一个已接受的 WebSocket 连接直到其关闭
    /// </summary>
    /// <param name="socket">已接受的 WebSocket</param>
    /// <param name="userId">连接所属用户标识</param>
    /// <param name="isService">连接所属用户是否拥有客服工作台权限，决定是否接收所有用户的客服消息</param>
    /// <param name="cancellationToken">请求取消令牌</param>
    /// <returns>连接结束时完成的任务</returns>
    public async Task HandleAsync(
        WebSocket socket
        , long userId
        , bool isService
        , CancellationToken cancellationToken
    ) {
        Start();
        using var connection = new RealtimeConnection(userId, socket, isService);
        _ = _connections.TryAdd(connection.Id, connection);
        try {
            // 每次（重）连接都先推送当前后端版本，前端据此判断后端是否已升级
            await SendAsync(connection, "system-info", systemInfo.Info, cancellationToken).ConfigureAwait(false);
            var counts = await CountUnreadAsync([userId], cancellationToken).ConfigureAwait(false);
            await SendAsync(connection, "unread", new RealtimeUnreadResult(counts.GetValueOrDefault(userId), false), cancellationToken)
                .ConfigureAwait(false);
            await ReceiveLoopAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) {
            _logCanceled(logger, userId, exception);
        }
        catch (WebSocketException exception) {
            _logAborted(logger, userId, exception);
        }
        finally {
            _ = _connections.TryRemove(connection.Id, out _);
        }
    }

    /// <summary>
    ///     向指定用户的所有在线连接推送最新未读站内信数量，未在线的用户会被忽略
    /// </summary>
    /// <param name="userIds">目标用户标识集合</param>
    /// <param name="hasNew">本次推送是否由新消息触发</param>
    /// <returns>推送完成的任务</returns>
    public Task NotifyUnreadAsync(
        IEnumerable<long> userIds
        , bool hasNew
    ) {
        Start();
        return bus.PublishAsync(new RealtimeBusMessage("unread", [.. userIds.Distinct()], 0, hasNew, null, null));
    }

    /// <summary>
    ///     推送一条客服消息：会话所属用户的所有在线连接和所有客服连接都会收到
    /// </summary>
    /// <param name="ownerUserId">会话所属用户标识</param>
    /// <param name="payload">消息载荷</param>
    /// <returns>推送完成的任务</returns>
    public Task PushSupportMessageAsync(
        long ownerUserId
        , RealtimeSupportMessageResult payload
    ) {
        Start();
        return bus.PublishAsync(new RealtimeBusMessage("support-message", [], ownerUserId, false, payload, null));
    }

    /// <summary>
    ///     推送客服会话已读状态：会话所属用户的所有在线连接和所有客服连接都会收到
    /// </summary>
    /// <param name="ownerUserId">会话所属用户标识</param>
    /// <param name="conversationId">会话主键</param>
    /// <param name="userUnreadCount">会话当前用户未读数量</param>
    /// <param name="serviceUnreadCount">会话当前客服未读数量</param>
    /// <returns>推送完成的任务</returns>
    public Task PushSupportReadAsync(
        long ownerUserId
        , long conversationId
        , int userUnreadCount
        , int serviceUnreadCount
    ) {
        var payload = new RealtimeSupportReadResult(conversationId, ownerUserId, userUnreadCount, serviceUnreadCount);
        Start();
        return bus.PublishAsync(new RealtimeBusMessage("support-read", [], ownerUserId, false, null, payload));
    }

    /// <summary>
    ///     订阅推送总线，收到总线消息后只推送给本实例的本地连接；可重复调用，只会订阅一次。
    ///     应用启动时由托管服务调用，推送和接入连接时也会兜底调用，避免因漏调导致推送静默丢失
    /// </summary>
    public void Start() {
        if (Interlocked.Exchange(ref _subscribed, 1) == 0) {
            bus.Subscribe(DeliverAsync);
        }
    }

    /// <summary>
    ///     持续接收客户端消息：收到文本 ping 回复 pong，收到关闭帧后完成关闭握手
    /// </summary>
    /// <param name="connection">连接</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接关闭时完成的任务</returns>
    private static async Task ReceiveLoopAsync(
        RealtimeConnection connection
        , CancellationToken cancellationToken
    ) {
        var buffer = new byte[_RECEIVE_BUFFER_SIZE];
        while (connection.Socket.State == WebSocketState.Open) {
            var result = await connection.Socket.ReceiveAsync([with(buffer)], cancellationToken).ConfigureAwait(false);
            switch (result.MessageType) {
                case WebSocketMessageType.Close:
                    await connection
                        .Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None)
                        .ConfigureAwait(false);
                    return;
                case WebSocketMessageType.Text when result.EndOfMessage
                                                    && string.Equals(
                                                        Encoding.UTF8.GetString(buffer, 0, result.Count), "ping", StringComparison.Ordinal
                                                    ):
                    await SendTextAsync(connection, "pong", cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>
    ///     向连接发送一条信封消息
    /// </summary>
    /// <param name="connection">连接</param>
    /// <param name="type">消息类型</param>
    /// <param name="data">消息载荷</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送完成的任务</returns>
    private static Task SendAsync(
        RealtimeConnection connection
        , string type
        , object data
        , CancellationToken cancellationToken
    ) {
        return SendTextAsync(connection, JsonSerializer.Serialize(new RealtimeEnvelope(type, data), _jsonOptions), cancellationToken);
    }

    /// <summary>
    ///     向连接发送文本帧，连接已不是打开状态时直接忽略
    /// </summary>
    /// <param name="connection">连接</param>
    /// <param name="text">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送完成的任务</returns>
    private static async Task SendTextAsync(
        RealtimeConnection connection
        , string text
        , CancellationToken cancellationToken
    ) {
        if (connection.Socket.State != WebSocketState.Open) {
            return;
        }

        await connection.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await connection.Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally {
            _ = connection.SendLock.Release();
        }
    }

    /// <summary>
    ///     一次查询统计多个用户的未读站内信数量
    /// </summary>
    /// <param name="userIds">用户标识集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户标识到未读数量的映射，没有未读的用户不在其中</returns>
    private async Task<Dictionary<long, int>> CountUnreadAsync(
        IReadOnlyCollection<long> userIds
        , CancellationToken cancellationToken
    ) {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db
            .UserMessages.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && !x.IsDeleted && !x.IsRead)
            .GroupBy(x => x.UserId)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(x => x.UserId, x => x.Count);
    }

    /// <summary>
    ///     处理总线消息，只推送给本实例上的连接
    /// </summary>
    /// <param name="message">总线消息</param>
    /// <returns>推送完成的任务</returns>
    private Task DeliverAsync(RealtimeBusMessage message) {
        return message.Kind switch
        {
            "unread" => DeliverUnreadAsync(message.UserIds, message.HasNew)
            , "support-message" when message.Message is not null => DeliverSupportMessageAsync(message.OwnerUserId, message.Message)
            , "support-read" when message.Read is not null => DeliverSupportReadAsync(message.OwnerUserId, message.Read)
            , _ => Task.CompletedTask
        };
    }

    /// <summary>
    ///     向本实例上的会话所属用户连接和客服连接推送客服消息
    /// </summary>
    /// <param name="ownerUserId">会话所属用户标识</param>
    /// <param name="payload">消息载荷</param>
    /// <returns>推送完成的任务</returns>
    private async Task DeliverSupportMessageAsync(
        long ownerUserId
        , RealtimeSupportMessageResult payload
    ) {
        foreach (var connection in _connections.Values.Where(x => x.UserId == ownerUserId || x.IsService).ToList()) {
            try {
                await SendAsync(connection, "support-message", payload, CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException exception) {
                _logSupportPushFailed(logger, connection.UserId, exception);
            }
            catch (ObjectDisposedException exception) {
                _logPushSkipped(logger, connection.UserId, exception);
            }
        }
    }

    /// <summary>
    ///     向本实例上的会话所属用户连接和客服连接推送已读状态
    /// </summary>
    /// <param name="ownerUserId">会话所属用户标识</param>
    /// <param name="payload">已读载荷</param>
    /// <returns>推送完成的任务</returns>
    private async Task DeliverSupportReadAsync(
        long ownerUserId
        , RealtimeSupportReadResult payload
    ) {
        foreach (var connection in _connections.Values.Where(x => x.UserId == ownerUserId || x.IsService).ToList()) {
            try {
                await SendAsync(connection, "support-read", payload, CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException exception) {
                _logSupportPushFailed(logger, connection.UserId, exception);
            }
            catch (ObjectDisposedException exception) {
                _logPushSkipped(logger, connection.UserId, exception);
            }
        }
    }

    /// <summary>
    ///     向本实例上属于目标用户的连接推送最新未读数
    /// </summary>
    /// <param name="userIds">目标用户标识集合</param>
    /// <param name="hasNew">本次推送是否由新消息触发</param>
    /// <returns>推送完成的任务</returns>
    private async Task DeliverUnreadAsync(
        IEnumerable<long> userIds
        , bool hasNew
    ) {
        var targets = userIds.ToHashSet();
        var connections = _connections.Values.Where(x => targets.Contains(x.UserId)).ToList();
        if (connections.Count == 0) {
            return;
        }

        var counts = await CountUnreadAsync([.. connections.Select(x => x.UserId).Distinct()], CancellationToken.None).ConfigureAwait(false);
        foreach (var connection in connections) {
            try {
                await SendAsync(
                        connection, "unread", new RealtimeUnreadResult(counts.GetValueOrDefault(connection.UserId), hasNew), CancellationToken.None
                    )
                    .ConfigureAwait(false);
            }
            catch (WebSocketException exception) {
                _logPushFailed(logger, connection.UserId, exception);
            }
            catch (ObjectDisposedException exception) {
                _logPushSkipped(logger, connection.UserId, exception);
            }
        }
    }
}