using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表动态查询请求
/// </summary>
public sealed class DynamicQueryRequest
{
    /// <summary>
    ///     当前页码
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Current { get; init; } = 1;

    /// <summary>
    ///     动态筛选根节点
    /// </summary>
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     每页记录数
    /// </summary>
    [Range(1, 100)]
    public int Size { get; init; } = 20;

    /// <summary>
    ///     排序字段名称
    /// </summary>
    [StringLength(200)]
    public string? SortField { get; init; }

    /// <summary>
    ///     排序方向，支持 asc 或 desc
    /// </summary>
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortOrder must be asc or desc")]
    public string? SortOrder { get; init; }
}