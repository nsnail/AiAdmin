namespace AiAdmin.Api.Models;

/// <summary>
///     角色与菜单关联实体
/// </summary>
public sealed class RoleMenu : EntityBase
{
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