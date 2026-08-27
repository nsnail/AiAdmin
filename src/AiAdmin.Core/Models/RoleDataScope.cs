namespace AiAdmin.Api.Models;

/// <summary>
///     角色数据权限范围
/// </summary>
public enum RoleDataScope
{
    /// <summary>全部数据</summary>
    All = 0

    ,

    /// <summary>本部门数据</summary>
    Department = 1

    ,

    /// <summary>本部门和子部门数据</summary>
    DepartmentAndChildren = 2

    ,

    /// <summary>本人数据</summary>
    Self = 3
}