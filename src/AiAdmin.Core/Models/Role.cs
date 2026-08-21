// 定义角色、菜单、接口及其关联关系的实体模型。
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统角色实体
/// </summary>
public sealed class Role : EntityBase
{
    /// <summary>
    ///     角色编码
    /// </summary>
    [ListFilter("listFilter.role.code", Placeholder = "listFilter.placeholder.roleCode", Span = 3)]
    public required string Code { get; set; }

    /// <summary>
    ///     角色数据权限范围代码
    /// </summary>
    [ListFilter(
        "listFilter.role.dataScope", "select"
        , Options =
        [
            "all:listFilter.option.allData", "department:listFilter.option.departmentData"
            , "department_and_children:listFilter.option.departmentAndChildren", "self:listFilter.option.ownData"
        ], Span = 4
    )]
    public string DataScope { get; set; } = RoleDataScope.SELF;

    /// <summary>
    ///     角色描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用角色
    /// </summary>
    [ListFilter("listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], Span = 2)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     角色名称
    /// </summary>
    [ListFilter("listFilter.role.name", Placeholder = "listFilter.placeholder.roleName", Span = 3, Sort = 0)]
    public required string Name { get; set; }

    /// <summary>
    ///     角色接口关联集合
    /// </summary>
    public ICollection<RoleApi> RoleApis { get; set; } = [];

    /// <summary>
    ///     角色菜单关联集合
    /// </summary>
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public ICollection<UserRole> UserRoles { get; init; } = [];
}