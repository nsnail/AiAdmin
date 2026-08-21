using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Logging;

/// <summary>
///     系统日志分页查询请求
/// </summary>
public sealed class SystemLogQueryRequest
{
    /// <summary>
    ///     当前页码
    /// </summary>
    public int Current { get; set; } = 1;

    /// <summary>
    ///     动态查询条件
    /// </summary>
    public DynamicFilter? DynamicFilter { get; set; }

    /// <summary>
    ///     每页记录数
    /// </summary>
    public int Size { get; set; } = 20;

    /// <summary>
    ///     排序字段
    /// </summary>
    public string? SortField { get; set; }

    /// <summary>
    ///     排序方向
    /// </summary>
    public string? SortOrder { get; set; }
}