using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统菜单实体
/// </summary>
public sealed class Menu : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     前端组件路径
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    ///     创建时间
    /// </summary>
    [ListFilter(IsVisible = false)]
    public override DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     菜单主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用菜单
    /// </summary>
    [ListFilter(
        "listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], GroupCount = true
    )]
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
    public string ParentName { get; set; } = string.Empty;

    /// <summary>
    ///     菜单路由路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     角色菜单关联集合
    /// </summary>
    public ICollection<RoleMenu> RoleMenus { get; init; } = [];

    /// <summary>
    ///     菜单排序值
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}