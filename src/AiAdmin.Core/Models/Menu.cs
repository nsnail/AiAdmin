// 定义角色、菜单、接口及其关联关系的实体模型。
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

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