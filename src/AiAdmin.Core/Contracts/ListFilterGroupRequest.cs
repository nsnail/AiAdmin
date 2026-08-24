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
}