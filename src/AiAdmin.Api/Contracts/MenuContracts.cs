using System.Text.Json;
using System.Text.Json.Serialization;

// 定义菜单树、菜单维护和角色菜单授权的数据传输模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     菜单树节点响应
/// </summary>
public sealed record MenuItemResult(
    long Id
    , string Name
    , string Path
    , string Component
    , string ParentName
    , int Sort
    , JsonElement Meta
    , IReadOnlyList<MenuItemResult> Children);

/// <summary>
///     保存角色菜单授权请求
/// </summary>
public sealed class SaveRoleMenusRequest
{
    /// <summary>
    ///     菜单主键集合
    /// </summary>
    public long[] MenuIds { get; init; } = [];
}

/// <summary>
///     菜单新增或修改请求
/// </summary>
public sealed class SaveMenuRequest
{
    /// <summary>
    ///     前端组件路径
    /// </summary>
    public string Component { get; init; } = string.Empty;

    /// <summary>
    ///     是否启用菜单
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     菜单元数据
    /// </summary>
    [JsonRequired]
    public JsonElement Meta { get; init; }

    /// <summary>
    ///     菜单名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     父级菜单名称
    /// </summary>
    public string ParentName { get; init; } = string.Empty;

    /// <summary>
    ///     菜单路由路径
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    ///     菜单排序值
    /// </summary>
    [JsonRequired]
    public int Sort { get; init; }
}

/// <summary>
///     种子菜单节点请求
/// </summary>
public sealed class MenuItemRequest
{
    /// <summary>
    ///     子菜单集合
    /// </summary>
    public MenuItemRequest[] Children { get; init; } = [];

    /// <summary>
    ///     前端组件路径
    /// </summary>
    public string Component { get; init; } = string.Empty;

    /// <summary>
    ///     菜单元数据
    /// </summary>
    public JsonElement Meta { get; init; }

    /// <summary>
    ///     菜单名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     父级菜单名称
    /// </summary>
    public string ParentName { get; init; } = string.Empty;

    /// <summary>
    ///     菜单路由路径
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    ///     菜单排序值
    /// </summary>
    public int Sort { get; init; }
}