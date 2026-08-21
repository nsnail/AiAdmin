namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 缓存键内容
/// </summary>
public sealed record RedisCacheValueResult(
    string Key
    , string Type
    , string Value
    , long TimeToLiveMilliseconds
    , long MemoryBytes
    , long Length);