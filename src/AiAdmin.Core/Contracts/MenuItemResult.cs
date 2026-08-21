using System.Text.Json;

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