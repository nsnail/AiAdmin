// 定义角色可授予的数据权限范围

namespace AiAdmin.Api.Models;

/// <summary>
///     角色数据权限范围代码
/// </summary>
public static class RoleDataScope
{
    /// <summary>
    ///     全部数据
    /// </summary>
    public const string ALL = "all";

    /// <summary>
    ///     本部门数据
    /// </summary>
    public const string DEPARTMENT = "department";

    /// <summary>
    ///     本部门和子部门数据
    /// </summary>
    public const string DEPARTMENT_AND_CHILDREN = "department_and_children";

    /// <summary>
    ///     本人数据
    /// </summary>
    public const string SELF = "self";

    /// <summary>
    ///     判断数据权限范围代码是否有效
    /// </summary>
    /// <param name="dataScope">数据权限范围代码</param>
    /// <returns>代码有效时返回 true</returns>
    public static bool IsValid(string dataScope) {
        return dataScope is ALL or DEPARTMENT or DEPARTMENT_AND_CHILDREN or SELF;
    }
}