using System.Text.Json;

// 定义菜单树、菜单维护和角色菜单授权的数据传输模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     菜单树节点响应
/// </summary>
public sealed record MenuItemResult(
    long Id
    , DateTimeOffset CreatedAt
    , string Name
    , string Path
    , string Component
    , string ParentName
    , int Sort
    , bool IsEnabled
    , JsonElement Meta
    , IReadOnlyList<MenuItemResult> Children);