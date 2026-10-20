namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理失效中间表映射结果
/// </summary>
/// <param name="UserRoles">删除的用户角色映射数量</param>
/// <param name="UserDepartments">删除的用户部门映射数量</param>
/// <param name="RoleMenus">删除的角色菜单映射数量</param>
/// <param name="RoleMenuAuths">删除的角色菜单按钮权限数量</param>
/// <param name="RoleApis">删除的角色接口映射数量</param>
public sealed record CleanupOrphanMappingsResult(int UserRoles, int UserDepartments, int RoleMenus, int RoleMenuAuths, int RoleApis)
{
    /// <summary>
    ///     Gets 本次删除的映射总数
    /// </summary>
    public int Total => UserRoles + UserDepartments + RoleMenus + RoleMenuAuths + RoleApis;
}