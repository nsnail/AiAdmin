using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Logging;

/// <summary>
///     系统日志分页查询请求
/// </summary>
public sealed class SystemLogQueryRequest
{
    /// <summary>
    ///     日志分类
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     当前页码
    /// </summary>
    public int Current { get; set; } = 1;

    /// <summary>
    ///     日志关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    ///     指定搜索字段
    /// </summary>
    public string? SearchField { get; set; }

    /// <summary>
    ///     动态查询条件
    /// </summary>
    public DynamicFilter? DynamicFilter { get; set; }

    /// <summary>
    ///     日志级别
    /// </summary>
    public string? Level { get; set; }

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