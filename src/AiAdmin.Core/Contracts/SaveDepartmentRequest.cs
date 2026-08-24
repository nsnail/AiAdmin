using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     部门新增或修改请求
/// </summary>
public sealed class SaveDepartmentRequest
{
    /// <summary>
    ///     部门编码
    /// </summary>
    [Required]
    [StringLength(50)]
    [RegularExpression("^[A-Z0-9]+$")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     部门描述
    /// </summary>
    [StringLength(500)]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    ///     是否启用部门
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     部门名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     父部门主键
    /// </summary>
    public long? ParentId { get; init; }

    /// <summary>
    ///     同级显示顺序
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Sort { get; init; }
}