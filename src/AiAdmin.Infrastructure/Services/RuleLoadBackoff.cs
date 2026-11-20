namespace AiAdmin.Api.Services;

/// <summary>
///     规则加载失败后的进程内退避：加载失败后在一小段时间内不再尝试加载，避免每个请求都去访问故障的数据库或 Redis，
///     把一次故障放大成每个请求一次必然失败的查询。退避状态只放在进程内存，不依赖 Redis，因为 Redis 本身可能就是故障源
/// </summary>
/// <param name="duration">一次失败后的退避时长</param>
public sealed class RuleLoadBackoff(TimeSpan duration)
{
    private long _retryAtTicks;

    /// <summary>
    ///     是否处于退避期，退避期内调用方应直接按没有规则处理，不再尝试加载
    /// </summary>
    public bool IsBackingOff => Environment.TickCount64 < Interlocked.Read(ref _retryAtTicks);

    /// <summary>
    ///     记录一次加载失败并进入退避期
    /// </summary>
    /// <returns>本次是否是进入退避期的第一次失败，用于每个退避周期只记一条日志；并发的多次失败只有一次返回 true</returns>
    public bool Fail() {
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref _retryAtTicks);
        var next = now + (long)duration.TotalMilliseconds;
        return now >= previous && Interlocked.CompareExchange(ref _retryAtTicks, next, previous) == previous;
    }

    /// <summary>
    ///     记录一次加载成功并结束退避
    /// </summary>
    public void Succeed() {
        _ = Interlocked.Exchange(ref _retryAtTicks, 0);
    }
}