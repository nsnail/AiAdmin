using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     仪表盘操作日志统计的 Redis 缓存：统计需要扫描今天和昨天两天的日分表，日志量大时耗时明显，
///     计划作业定时预热，页面只读缓存；缓存未命中时当场计算一次并写入，用 Redis 锁避免多个请求或多个实例同时重复扫表
/// </summary>
/// <param name="cache">分布式缓存</param>
/// <param name="connectionMultiplexer">Redis 连接复用器，用于计算锁</param>
/// <param name="scopeFactory">服务作用域工厂，计算时在独立作用域里取统计服务，不依赖当前请求</param>
public sealed class DashboardRequestStatsCache(
    IDistributedCache cache
    , IConnectionMultiplexer connectionMultiplexer
    , IServiceScopeFactory scopeFactory)
{
    /// <summary>
    ///     预热使用的时区偏移分钟数，固定为 UTC+8，与服务器时区一致
    /// </summary>
    public const int WARM_OFFSET_MINUTES = 480;

    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan _lockLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _lockWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///     读取缓存，未命中时当场计算一次并写入；同一时区偏移同一时刻只有一个请求在计算，其他请求等待后直接读取结果
    /// </summary>
    /// <param name="offsetMinutes">客户端相对 UTC 的时区偏移分钟数，范围 -840 到 840，超出会被限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作日志统计结果</returns>
    public async Task<DashboardRequestStatsResult> GetOrBuildAsync(
        int offsetMinutes
        , CancellationToken cancellationToken
    ) {
        offsetMinutes = Math.Clamp(offsetMinutes, -840, 840);
        var cached = await ReadAsync(offsetMinutes, cancellationToken).ConfigureAwait(false);
        if (cached is not null) {
            return cached;
        }

        var database = connectionMultiplexer.GetDatabase();
        RedisKey lockKey = string.Create(CultureInfo.InvariantCulture, $"{RedisKeyPrefix.VALUE}dashboard-request-stats:lock:{offsetMinutes}");
        RedisValue token = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow + _lockWait;
        while (true) {
            if (await database.StringSetAsync(lockKey, token, _lockLifetime, When.NotExists).ConfigureAwait(false)) {
                try {
                    return await BuildAndStoreAsync(offsetMinutes, cancellationToken).ConfigureAwait(false);
                }
                finally {
                    await ReleaseAsync(database, lockKey, token).ConfigureAwait(false);
                }
            }

            // 别的请求正在计算：等它写入缓存后直接读取；等待超时就自己算一次，不让页面一直转圈
            await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
            cached = await ReadAsync(offsetMinutes, cancellationToken).ConfigureAwait(false);
            if (cached is not null) {
                return cached;
            }

            if (DateTime.UtcNow >= deadline) {
                return await BuildAndStoreAsync(offsetMinutes, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     刷新预热组合（UTC+8）的缓存，供计划作业调用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次写入的缓存条数</returns>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken) {
        _ = await BuildAndStoreAsync(WARM_OFFSET_MINUTES, cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <summary>
    ///     生成缓存键
    /// </summary>
    /// <param name="offsetMinutes">时区偏移分钟数</param>
    /// <returns>缓存键，不含实例前缀</returns>
    private static string CacheKey(int offsetMinutes) {
        return string.Create(CultureInfo.InvariantCulture, $"dashboard-request-stats:{offsetMinutes}");
    }

    /// <summary>
    ///     释放计算锁：只有锁还是自己持有时才删除，避免误删别人后来拿到的锁
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">锁键</param>
    /// <param name="token">持有者标识</param>
    /// <returns>异步释放任务</returns>
    private static async Task ReleaseAsync(
        IDatabase database
        , RedisKey key
        , RedisValue token
    ) {
        const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
        _ = await database.ScriptEvaluateAsync(script, [key], [token]).ConfigureAwait(false);
    }

    /// <summary>
    ///     在独立作用域里计算统计并写入缓存
    /// </summary>
    /// <param name="offsetMinutes">时区偏移分钟数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>刚计算出的统计结果</returns>
    private async Task<DashboardRequestStatsResult> BuildAndStoreAsync(
        int offsetMinutes
        , CancellationToken cancellationToken
    ) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DashboardRequestStatsService>();
        var result = await service.BuildAsync(offsetMinutes, cancellationToken).ConfigureAwait(false);
        await cache
            .SetStringAsync(
                CacheKey(offsetMinutes), JsonSerializer.Serialize(result, _json)
                , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheLifetime }, cancellationToken
            )
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>
    ///     读取缓存并反序列化，缓存不存在或内容损坏时返回空
    /// </summary>
    /// <param name="offsetMinutes">时区偏移分钟数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存中的统计结果，未命中时为空</returns>
    private async Task<DashboardRequestStatsResult?> ReadAsync(
        int offsetMinutes
        , CancellationToken cancellationToken
    ) {
        var text = await cache.GetStringAsync(CacheKey(offsetMinutes), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text)) {
            return null;
        }

        try {
            return JsonSerializer.Deserialize<DashboardRequestStatsResult>(text, _json);
        }
        catch (JsonException) {
            return null;
        }
    }
}