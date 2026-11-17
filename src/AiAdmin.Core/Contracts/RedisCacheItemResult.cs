namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 集合类缓存键中的单个元素，按类型填充不同字段
/// </summary>
/// <param name="Index">元素序号：List 为下标，SortedSet 为排名，Hash、Set、Stream 为本次读取中的位置</param>
/// <param name="Field">Hash 的字段名、Stream 条目的 ID，其他类型为空</param>
/// <param name="Value">元素值：List、Set 的成员，Hash 的字段值，SortedSet 的成员，Stream 条目的字段键值对 JSON</param>
/// <param name="Score">SortedSet 成员的分数，其他类型为空</param>
public sealed record RedisCacheItemResult(long Index, string? Field, string Value, double? Score);