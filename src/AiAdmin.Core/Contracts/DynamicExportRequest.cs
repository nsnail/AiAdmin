using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表导出请求，只包含动态筛选与排序，导出条数由后端按系统设置的上限决定，不接收分页参数
/// </summary>
public sealed class DynamicExportRequest
{
    /// <summary>
    ///     动态筛选根节点
    /// </summary>
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     父资源主键，嵌套列表（例如字典目录下的字典项、作业下的执行记录）导出时使用
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? ParentId { get; init; }

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