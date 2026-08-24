namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户数据导出请求
/// </summary>
public sealed class UserExportRequest
{
    /// <summary>
    ///     动态筛选条件
    /// </summary>
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     排序字段
    /// </summary>
    public string? SortField { get; init; }

    /// <summary>
    ///     排序方向
    /// </summary>
    public string? SortOrder { get; init; }
}