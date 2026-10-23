using System.Text.Json;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Contracts;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     基于 Redis Pub/Sub 的实时推送总线，发布实例与订阅实例通过同一个频道交换消息
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="logger">日志记录器</param>
public sealed class RedisRealtimeBus(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisRealtimeBus> logger) : IRealtimeBus
{
    private static readonly RedisChannel _channel = RedisChannel.Literal($"{RedisKeyPrefix.VALUE}realtime:bus");
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, Exception?> _logHandlerFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(3322, "RealtimeBusHandlerFailed"), "Realtime bus handler failed"
    );

    private static readonly Action<ILogger, Exception?> _logInvalidMessage = LoggerMessage.Define(
        LogLevel.Warning, new EventId(3321, "RealtimeBusInvalidMessage"), "Realtime bus message is invalid"
    );

    /// <summary>
    ///     把消息序列化后发布到 Redis 频道
    /// </summary>
    /// <param name="message">总线消息</param>
    /// <returns>发布完成的任务</returns>
    public async Task PublishAsync(RealtimeBusMessage message) {
        _ = await connectionMultiplexer
            .GetSubscriber()
            .PublishAsync(_channel, new RedisValue(JsonSerializer.Serialize(message, _jsonOptions)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     订阅 Redis 频道，消息按到达顺序依次处理；StackExchange.Redis 会在断线重连后自动恢复订阅
    /// </summary>
    /// <param name="handler">消息处理委托</param>
    public void Subscribe(Func<RealtimeBusMessage, Task> handler) {
        connectionMultiplexer.GetSubscriber().Subscribe(_channel).OnMessage(message => DispatchAsync(message.Message, handler));
    }

    /// <summary>
    ///     反序列化并交给处理委托，格式错误或处理失败只记录日志，不影响后续消息
    /// </summary>
    /// <param name="value">频道消息内容</param>
    /// <param name="handler">消息处理委托</param>
    /// <returns>处理完成的任务</returns>
    private async Task DispatchAsync(
        RedisValue value
        , Func<RealtimeBusMessage, Task> handler
    ) {
        // 收到总线消息到推送完成之间属于在途工作，安全停机不能在此期间放行
        using var lease = ShutdownGate.Enter(ShutdownActivity.Background);
        RealtimeBusMessage? message;
        try {
            message = JsonSerializer.Deserialize<RealtimeBusMessage>(value.ToString(), _jsonOptions);
        }
        catch (JsonException exception) {
            _logInvalidMessage(logger, exception);
            return;
        }

        if (message is null) {
            return;
        }

        try {
            await handler(message).ConfigureAwait(false);
        }
        catch (Exception exception) {
            _logHandlerFailed(logger, exception);
        }
    }
}