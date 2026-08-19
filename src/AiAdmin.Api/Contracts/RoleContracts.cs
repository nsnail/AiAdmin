using System.ComponentModel.DataAnnotations;

// 定义角色查询和保存请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色列表项
/// </summary>
public sealed record RoleListItem(long RoleId, string RoleName, string RoleCode, string Description, bool Enabled, DateTime CreateTime);

/// <summary>
///     角色新增或修改请求
/// </summary>
public sealed class SaveRoleRequest
{
    /// <summary>
    ///     角色描述
    /// </summary>
    [StringLength(200)]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    ///     是否启用角色
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    ///     角色编码
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string RoleCode { get; init; } = string.Empty;

    /// <summary>
    ///     角色名称
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string RoleName { get; init; } = string.Empty;
}