using System.Text.Json;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     菜单树节点响应
/// </summary>
/// <param name="Id">菜单标识</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="UpdatedAt">更新时间</param>
/// <param name="Name">菜单名称</param>
/// <param name="Path">菜单路径</param>
/// <param name="Component">组件路径</param>
/// <param name="ParentName">父菜单名称</param>
/// <param name="Sort">排序值</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="Meta">菜单元数据</param>
/// <param name="Children">子菜单</param>
public sealed record MenuItemResult(
    long Id
    , DateTimeOffset CreatedAt
    , DateTimeOffset? UpdatedAt
    , string Name
    , string Path
    , string Component
    , string ParentName
    , int Sort
    , bool IsEnabled
    , JsonElement Meta
    , IReadOnlyList<MenuItemResult> Children);