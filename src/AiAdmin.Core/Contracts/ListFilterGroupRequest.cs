namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选分组统计请求
/// </summary>
public sealed class ListFilterGroupRequest
{
    /// <summary>
    ///     当前动态筛选条件
    /// </summary>
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     每个分组返回的最大选项数量，未指定时使用默认值
    /// </summary>
    public int? MaxOptions { get; init; }

    /// <summary>
    ///     父资源主键，嵌套列表查询时使用
    /// </summary>
    public long? ParentId { get; init; }
}