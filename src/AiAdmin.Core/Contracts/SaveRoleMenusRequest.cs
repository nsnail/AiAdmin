namespace AiAdmin.Api.Contracts;

/// <summary>
///     保存角色菜单授权请求
/// </summary>
public sealed class SaveRoleMenusRequest
{
    /// <summary>
    ///     授予的按钮权限集合，所属菜单必须同时在菜单主键集合中，标识必须存在于该菜单的 authList
    /// </summary>
    public List<RoleMenuAuthItem> Auths { get; init; } = [];

    /// <summary>
    ///     角色主键
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    ///     菜单主键集合
    /// </summary>
    public long[] MenuIds { get; init; } = [];
}