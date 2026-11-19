namespace AiAdmin.Api.Services;

/// <summary>
///     就绪探测结果
/// </summary>
/// <param name="Ready">数据库和 Redis 是否都在超时内应答</param>
/// <param name="DatabaseMilliseconds">数据库往返耗时，毫秒</param>
/// <param name="CacheMilliseconds">Redis 往返耗时，毫秒</param>
public sealed record ReadinessResult(bool Ready, long DatabaseMilliseconds, long CacheMilliseconds);