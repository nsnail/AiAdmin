using AiAdmin.Api.Caching;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     计划作业手动触发队列，使用 Redis 集合记录待立即执行的作业，由后台调度器消费并跳过 Cron 判断
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
public sealed class ScheduledJobManualTriggerQueue(IConnectionMultiplexer connectionMultiplexer)
{
    private static readonly RedisKey _key = $"{RedisKeyPrefix.VALUE}scheduled-job:manual-triggers";

    /// <summary>
    ///     登记一个待立即执行的作业
    /// </summary>
    /// <param name="jobId">计划作业编号</param>
    /// <returns>异步登记任务</returns>
    public async Task EnqueueAsync(long jobId) {
        _ = await connectionMultiplexer.GetDatabase().SetAddAsync(_key, jobId).ConfigureAwait(false);
    }

    /// <summary>
    ///     读取所有待立即执行的作业编号
    /// </summary>
    /// <returns>待执行作业编号集合</returns>
    public async Task<HashSet<long>> GetPendingAsync() {
        var members = await connectionMultiplexer.GetDatabase().SetMembersAsync(_key).ConfigureAwait(false);
        return [.. members.Select(x => (long)x)];
    }

    /// <summary>
    ///     移除一个已处理的手动触发请求
    /// </summary>
    /// <param name="jobId">计划作业编号</param>
    /// <returns>异步移除任务</returns>
    public async Task RemoveAsync(long jobId) {
        _ = await connectionMultiplexer.GetDatabase().SetRemoveAsync(_key, jobId).ConfigureAwait(false);
    }
}