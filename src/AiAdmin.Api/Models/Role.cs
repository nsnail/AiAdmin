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
    [ListFilter("listFilter.role.code", Placeholder = "listFilter.placeholder.roleCode")]
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
        ]
    )]
    public string DataScope { get; set; } = RoleDataScope.SELF;

    /// <summary>
    ///     角色描述
    /// </summary>
    [ListFilter("listFilter.role.description", Placeholder = "listFilter.placeholder.description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用角色
    /// </summary>
    [ListFilter("listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"])]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     角色名称
    /// </summary>
    [ListFilter("listFilter.role.name", Placeholder = "listFilter.placeholder.roleName")]
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

/// <summary>
///     系统接口实体
/// </summary>
public sealed class ApiEndpoint : EntityBase
{
    /// <summary>
    ///     操作方法名称
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    ///     是否允许匿名访问
    /// </summary>
    [ListFilter("listFilter.api.allowAnonymous", "select", Options = ["true:listFilter.option.yes", "false:listFilter.option.no"])]
    public bool AllowAnonymous { get; set; }

    /// <summary>
    ///     控制器代码名称
    /// </summary>
    [ListFilter("listFilter.api.controller", Placeholder = "listFilter.placeholder.controller")]
    public string Controller { get; set; } = string.Empty;

    /// <summary>
    ///     控制器显示名称
    /// </summary>
    [ListFilter("listFilter.api.controllerName", Placeholder = "listFilter.placeholder.controllerName")]
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>
    ///     接口主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     HTTP 请求方法
    /// </summary>
    [ListFilter(
        "listFilter.api.method", "select"
        , Options =
        [
            "GET:listFilter.option.get", "POST:listFilter.option.post", "PUT:listFilter.option.put", "PATCH:listFilter.option.patch"
            , "DELETE:listFilter.option.delete"
        ]
    )]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    ///     接口显示名称
    /// </summary>
    [ListFilter("listFilter.api.name", Placeholder = "listFilter.placeholder.apiName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     接口路由路径
    /// </summary>
    [ListFilter("listFilter.api.path", Placeholder = "listFilter.placeholder.path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     角色接口关联集合
    /// </summary>
    public ICollection<RoleApi> RoleApis { get; init; } = [];
}

/// <summary>
///     角色与接口关联实体
/// </summary>
public sealed class RoleApi : EntityBase
{
    /// <summary>
    ///     关联接口
    /// </summary>
    public ApiEndpoint ApiEndpoint { get; init; } = null!;

    /// <summary>
    ///     接口主键
    /// </summary>
    public long ApiEndpointId { get; init; }

    /// <summary>
    ///     关联角色
    /// </summary>
    public Role Role { get; init; } = null!;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long RoleId { get; init; }
}

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

/// <summary>
///     系统菜单实体
/// </summary>
public sealed class Menu : EntityBase
{
    /// <summary>
    ///     前端组件路径
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    ///     菜单主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用菜单
    /// </summary>
    [ListFilter("listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"])]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     菜单元数据 JSON
    /// </summary>
    public string MetaJson { get; set; } = "{}";

    /// <summary>
    ///     菜单名称
    /// </summary>
    [ListFilter("listFilter.menu.name", Placeholder = "listFilter.placeholder.menuName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     父级菜单名称
    /// </summary>
    [ListFilter("listFilter.menu.parentName", Placeholder = "listFilter.placeholder.parentMenu")]
    public string ParentName { get; set; } = string.Empty;

    /// <summary>
    ///     菜单路由路径
    /// </summary>
    [ListFilter("listFilter.menu.path", Placeholder = "listFilter.placeholder.path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     角色菜单关联集合
    /// </summary>
    public ICollection<RoleMenu> RoleMenus { get; init; } = [];

    /// <summary>
    ///     菜单排序值
    /// </summary>
    public int Sort { get; set; }
}

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