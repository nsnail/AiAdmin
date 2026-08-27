using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统角色实体
/// </summary>
public sealed class Role : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     角色编码
    /// </summary>
    [ListFilter("listFilter.role.code", Placeholder = "listFilter.placeholder.roleCode", Span = 4)]
    public required string Code { get; set; }

    /// <summary>
    ///     角色数据权限范围代码
    /// </summary>
    [ListFilter(
        "listFilter.role.dataScope", "select"
        , Options =
        [
            "0:listFilter.option.allData", "1:listFilter.option.departmentData", "2:listFilter.option.departmentAndChildren"
            , "3:listFilter.option.ownData"
        ], Span = 4, GroupCount = true
    )]
    public RoleDataScope DataScope { get; set; } = RoleDataScope.Self;

    /// <summary>
    ///     角色描述
    /// </summary>
    [ListFilter("listFilter.role.description", Placeholder = "listFilter.placeholder.description", Span = 4)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用角色
    /// </summary>
    [ListFilter(
        "listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], Span = 2
        , GroupCount = true
    )]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     角色名称
    /// </summary>
    [ListFilter("listFilter.role.name", Placeholder = "listFilter.placeholder.roleName", Span = 4, Sort = 0)]
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
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public ICollection<UserRole> UserRoles { get; init; } = [];
}