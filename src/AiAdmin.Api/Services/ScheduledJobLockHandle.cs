using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     计划作业 Redis 分布式锁句柄，释放时仅删除当前持有者创建的锁
/// </summary>
/// <param name="database">Redis 数据库</param>
/// <param name="key">锁键</param>
/// <param name="value">锁持有者令牌</param>
public sealed class ScheduledJobLockHandle(
    IDatabase database
    , RedisKey key
    , RedisValue value) : IAsyncDisposable
{
    private const string ReleaseScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private bool _isReleased;

    /// <summary>
    ///     释放当前持有的计划作业锁
    /// </summary>
    /// <returns>异步释放任务</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isReleased)
        {
            return;
        }

        _isReleased = true;
        _ = await database.ScriptEvaluateAsync(ReleaseScript, [key], [value]).ConfigureAwait(false);
    }
}