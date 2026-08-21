using System.Text.Json;

// 定义菜单树、菜单维护和角色菜单授权的数据传输模型。
namespace AiAdmin.Api.Contracts;

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