// 定义角色、菜单、接口及其关联关系的实体模型。

namespace AiAdmin.Api.Models;

/// <summary>
///     用户与角色关联实体
/// </summary>
public sealed class UserRole : EntityBase
{
    /// <summary>
    ///     关联角色
    /// </summary>
    public Role Role { get; init; } = null!;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long RoleId { get; init; }

    /// <summary>
    ///     关联用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     用户主键
    /// </summary>
    public long UserId { get; init; }
}