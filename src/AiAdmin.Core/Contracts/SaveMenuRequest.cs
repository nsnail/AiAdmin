using System.Text.Json;
using System.Text.Json.Serialization;

// 定义菜单树、菜单维护和角色菜单授权的数据传输模型。
namespace AiAdmin.Api.Contracts;

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