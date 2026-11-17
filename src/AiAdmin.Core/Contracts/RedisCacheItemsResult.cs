namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 集合类缓存键的一页元素
/// </summary>
/// <param name="Key">缓存键</param>
/// <param name="Type">数据类型</param>
/// <param name="Items">本页元素</param>
/// <param name="Total">元素总数，Stream 为条目数</param>
/// <param name="NextCursor">继续读取下一页所需的游标，没有更多数据时为空；List、SortedSet 为下一个起始下标，Hash、Set 为 SCAN 游标，Stream 为下一个起始条目 ID</param>
/// <param name="Truncated">单个元素超过长度上限被截断时为真</param>
public sealed record RedisCacheItemsResult(
    string Key
    , string Type
    , IReadOnlyList<RedisCacheItemResult> Items
    , long Total
    , string? NextCursor
    , bool Truncated);