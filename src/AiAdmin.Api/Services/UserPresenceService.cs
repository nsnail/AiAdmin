using System.Data.Common;
using System.Globalization;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     用户在线状态：Redis 保存每个实时连接的登记，判定用户此刻是否至少有一条有效连接；用户表的 IsOnline 和 LastOnlineAt 是它的可查询投影，
///     供列表的筛选、排序、分组统计和导出直接使用。每个连接登记带过期时间并定期续期，实例崩溃后连接自动过期；
///     用户表只在状态真正变化时才写（上线时只更新当前为离线的用户，下线时只更新当前为在线的用户），页面刷新或网络抖动反复重连不会反复写库；
///     实例崩溃遗留在用户表里的在线状态，由 <see cref="ReconcileAsync" /> 在计划作业里校正
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="scopeFactory">服务作用域工厂，在独立作用域里写库，不依赖当前请求</param>
/// <param name="logger">日志记录器</param>
public sealed class UserPresenceService(
    IConnectionMultiplexer connectionMultiplexer
    , IServiceScopeFactory scopeFactory
    , ILogger<UserPresenceService> logger) : IUserPresence
{
    /// <summary>
    ///     连接登记的有效期，必须大于续期间隔，实例崩溃后最长多保留这么久的在线状态
    /// </summary>
    public static readonly TimeSpan ConnectionLifetime = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     连接登记的续期间隔
    /// </summary>
    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(20);

    private static readonly Action<ILogger, long, Exception?> _logRedisFailed = LoggerMessage.Define<long>(
        LogLevel.Warning, new EventId(3331, "UserPresenceRedisFailed"), "User presence for user {UserId} could not be updated in Redis"
    );

    private static readonly Action<ILogger, long, Exception?> _logWriteFailed = LoggerMessage.Define<long>(
        LogLevel.Warning, new EventId(3332, "UserPresenceWriteFailed"), "Online state of user {UserId} could not be saved"
    );

    /// <summary>
    ///     登记一个新连接：Redis 登记成功后，把用户表里仍是离线的用户置为在线；Redis 不可用时只记录日志，不影响实时连接本身
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步登记任务</returns>
    public async Task ConnectedAsync(
        long userId
        , Guid connectionId
    ) {
        try {
            await RenewCoreAsync(userId, connectionId).ConfigureAwait(false);
        }
        catch (RedisException exception) {
            _logRedisFailed(logger, userId, exception);
            return;
        }

        await SaveStateAsync(userId, true).ConfigureAwait(false);
    }

    /// <summary>
    ///     注销一个连接：用户已没有其他有效连接时，把用户表里仍是在线的用户置为离线并记录最后在线时间
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步注销任务</returns>
    public async Task DisconnectedAsync(
        long userId
        , Guid connectionId
    ) {
        try {
            var database = connectionMultiplexer.GetDatabase();
            var key = ConnectionsKey(userId);
            _ = await database.SortedSetRemoveAsync(key, connectionId.ToString("N")).ConfigureAwait(false);
            if (await CountAliveAsync(database, key).ConfigureAwait(false) > 0) {
                return;
            }
        }
        catch (RedisException exception) {
            // Redis 不可用时无法判断是否还有别的连接，保持库里的状态不变，由计划作业兜底校正
            _logRedisFailed(logger, userId, exception);
            return;
        }

        await SaveStateAsync(userId, false).ConfigureAwait(false);
    }

    /// <summary>
    ///     校正用户表里的在线状态：把库里标记为在线、但 Redis 里已经没有有效连接的用户置为离线，主要处理实例崩溃遗留的在线状态；供计划作业定时调用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次置为离线的用户数量</returns>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var markedOnline = await db
            .Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsOnline)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (markedOnline.Count == 0) {
            return 0;
        }

        var stale = new List<long>();
        var database = connectionMultiplexer.GetDatabase();
        foreach (var userId in markedOnline) {
            if (await CountAliveAsync(database, ConnectionsKey(userId)).ConfigureAwait(false) == 0) {
                stale.Add(userId);
            }
        }

        if (stale.Count == 0) {
            return 0;
        }

        var now = DateTime.UtcNow;

        // 以 IsOnline 为条件更新，期间重新上线的用户不会被误置为离线
        return await db
            .Users.IgnoreQueryFilters()
            .Where(x => stale.Contains(x.Id) && x.IsOnline)
            .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsOnline, false).SetProperty(row => row.LastOnlineAt, now), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     续期一个连接的登记，由连接的接收循环定期调用
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步续期任务</returns>
    public async Task RenewAsync(
        long userId
        , Guid connectionId
    ) {
        try {
            await RenewCoreAsync(userId, connectionId).ConfigureAwait(false);
        }
        catch (RedisException exception) {
            _logRedisFailed(logger, userId, exception);
        }
    }

    /// <summary>
    ///     生成某个用户的连接集合键，成员为连接编号，分值为过期时间的 Unix 秒
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <returns>Redis 键</returns>
    private static RedisKey ConnectionsKey(long userId) {
        return string.Create(CultureInfo.InvariantCulture, $"{RedisKeyPrefix.VALUE}presence:conn:{userId}");
    }

    /// <summary>
    ///     清理已过期的连接成员后返回仍有效的连接数
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">用户的连接集合键</param>
    /// <returns>有效连接数</returns>
    private static async Task<long> CountAliveAsync(
        IDatabase database
        , RedisKey key
    ) {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _ = await database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now).ConfigureAwait(false);
        return await database.SortedSetLengthAsync(key).ConfigureAwait(false);
    }

    /// <summary>
    ///     写入或续期连接成员，键本身也设置过期，避免用户再也不上线时遗留空键
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步任务</returns>
    private async Task RenewCoreAsync(
        long userId
        , Guid connectionId
    ) {
        var database = connectionMultiplexer.GetDatabase();
        var key = ConnectionsKey(userId);
        var expiresAt = DateTimeOffset.UtcNow.Add(ConnectionLifetime).ToUnixTimeSeconds();
        _ = await database.SortedSetAddAsync(key, connectionId.ToString("N"), expiresAt).ConfigureAwait(false);
        _ = await database.KeyExpireAsync(key, ConnectionLifetime * 2).ConfigureAwait(false);
    }

    /// <summary>
    ///     把状态写到用户表：只在状态真正变化时才写，上线时更新当前为离线的用户，下线时更新当前为在线的用户并记录最后在线时间
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="online">要写入的在线状态</param>
    /// <returns>异步写库任务，失败只记录日志</returns>
    private async Task SaveStateAsync(
        long userId
        , bool online
    ) {
        try {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = db.Users.IgnoreQueryFilters().Where(x => x.Id == userId && x.IsOnline != online);
            if (online) {
                _ = await users.ExecuteUpdateAsync(x => x.SetProperty(row => row.IsOnline, true)).ConfigureAwait(false);
            }
            else {
                var now = DateTime.UtcNow;
                _ = await users
                    .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsOnline, false).SetProperty(row => row.LastOnlineAt, now))
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or TimeoutException or DbException) {
            // 在线状态只是展示信息，写库失败不能影响实时连接的清理流程，只记录日志，残留的状态由计划作业校正
            _logWriteFailed(logger, userId, exception);
        }
    }
}