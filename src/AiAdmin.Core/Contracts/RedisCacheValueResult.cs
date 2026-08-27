namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 缓存键内容
/// </summary>
/// <param name="Key">缓存键</param>
/// <param name="Type">数据类型</param>
/// <param name="Value">缓存值</param>
/// <param name="TimeToLiveMilliseconds">剩余生存时间</param>
/// <param name="MemoryBytes">占用内存</param>
/// <param name="Length">数据长度</param>
public sealed record RedisCacheValueResult(string Key, string Type, string Value, long TimeToLiveMilliseconds, long MemoryBytes, long Length);