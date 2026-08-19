using System.Text.Json;

namespace AiAdmin.Api.Contracts;

public sealed record MenuItemResult(
    long Id
    , string Name
    , string Path
    , string Component
    , string ParentName
    , int Sort
    , JsonElement Meta
    , IReadOnlyList<MenuItemResult> Children);

public sealed class SaveRoleMenusRequest
{
    public long[] MenuIds { get; init; } = [];
}

public sealed class SaveMenuRequest
{
    public string Component { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public JsonElement Meta { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ParentName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int Sort { get; init; }
}

public sealed class MenuItemRequest
{
    public MenuItemRequest[] Children { get; init; } = [];
    public string Component { get; init; } = string.Empty;
    public JsonElement Meta { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ParentName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int Sort { get; init; }
}