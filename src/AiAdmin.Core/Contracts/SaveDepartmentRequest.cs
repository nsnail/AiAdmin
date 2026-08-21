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
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     部门邮箱
    /// </summary>
    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; init; }

    /// <summary>
    ///     是否启用部门
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     部门负责人
    /// </summary>
    [StringLength(50)]
    public string Leader { get; init; } = string.Empty;

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
    ///     部门电话
    /// </summary>
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    ///     同级显示顺序
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Sort { get; init; }
}