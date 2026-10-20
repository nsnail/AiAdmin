using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色在某个菜单下被授予的按钮权限
/// </summary>
public sealed class RoleMenuAuthItem
{
    /// <summary>
    ///     按钮权限标识
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AuthMark { get; init; } = string.Empty;

    /// <summary>
    ///     按钮所属菜单主键
    /// </summary>
    public long MenuId { get; init; }
}