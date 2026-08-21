namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 缓存键摘要
/// </summary>
public sealed record RedisCacheKeyResult(string Key, string Type, long TimeToLiveMilliseconds, long MemoryBytes, long Length);