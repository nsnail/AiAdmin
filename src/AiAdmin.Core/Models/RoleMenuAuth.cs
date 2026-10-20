using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Models;

/// <summary>
///     角色与菜单按钮权限关联实体，记录角色在某个菜单下被授予的按钮权限标识
/// </summary>
public sealed class RoleMenuAuth : EntityBase
{
    /// <summary>
    ///     按钮权限标识，对应菜单元数据 authList 中的 authMark
    /// </summary>
    [MaxLength(100)]
    public required string AuthMark { get; init; }

    /// <summary>
    ///     关联菜单
    /// </summary>
    public Menu Menu { get; init; } = null!;

    /// <summary>
    ///     菜单主键
    /// </summary>
    public long MenuId { get; init; }

    /// <summary>
    ///     关联角色
    /// </summary>
    public Role Role { get; init; } = null!;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long RoleId { get; init; }
}