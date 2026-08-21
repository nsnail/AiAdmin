using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色新增或修改请求
/// </summary>
public sealed class SaveRoleRequest
{
    /// <summary>
    ///     数据权限范围代码
    /// </summary>
    [Required]
    public string DataScope { get; init; } = RoleDataScope.SELF;

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