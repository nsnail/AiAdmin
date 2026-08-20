// 定义保存动态查询条件的接口契约

using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     保存查询条件请求
/// </summary>
public sealed class SaveQueryRequest
{
    /// <summary>
    ///     动态筛选根节点
    /// </summary>
    [Required]
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     查询条件名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     当前页面路由
    /// </summary>
    [Required]
    [StringLength(300)]
    public string Route { get; init; } = string.Empty;
}

/// <summary>
///     已保存查询条件响应
/// </summary>
/// <param name="Id">查询条件主键</param>
/// <param name="Name">查询条件名称</param>
/// <param name="DynamicFilter">动态筛选根节点</param>
public sealed record SavedQueryResult(long Id, string Name, DynamicFilter DynamicFilter);