using System.Diagnostics;
using AiAdmin.Api.Caching;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     为计划作业状态变更提供跨实例 Redis 分布式锁
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
public sealed class ScheduledJobLockService(IConnectionMultiplexer connectionMultiplexer)
{
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _retryInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///     尝试在指定等待时间内获取计划作业锁
    /// </summary>
    /// <param name="jobId">计划作业编号</param>
    /// <param name="waitTimeout">最长等待时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功时返回锁句柄，否则返回空值</returns>
    public async Task<ScheduledJobLockHandle?> TryAcquireAsync(
        long jobId
        , TimeSpan waitTimeout
        , CancellationToken cancellationToken
    ) {
        var database = connectionMultiplexer.GetDatabase();
        RedisKey key = $"{RedisKeyPrefix.VALUE}scheduled-job:lock:{jobId}";
        RedisValue value = Guid.NewGuid().ToString("N");
        var startedAt = Stopwatch.GetTimestamp();
        while (true) {
            var acquired = await database
                .StringSetAsync(key, value, _lockExpiration, When.NotExists)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            if (acquired) {
                return new ScheduledJobLockHandle(database, key, value);
            }

            if (Stopwatch.GetElapsedTime(startedAt) >= waitTimeout) {
                return null;
            }

            await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}