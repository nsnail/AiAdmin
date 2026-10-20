using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiAdmin.Api.Services;

/// <summary>
///     菜单按钮权限工具，读取菜单元数据声明的 authList，并按角色已授予的按钮标识过滤
/// </summary>
public static class MenuAuthListFilter
{
    /// <summary>
    ///     按角色已授予的按钮权限过滤菜单元数据中的 authList，没有任何授予时 authList 为空数组，其余元数据原样保留
    /// </summary>
    /// <param name="metaJson">菜单元数据 JSON</param>
    /// <param name="menuId">菜单主键</param>
    /// <param name="granted">角色已授予的（菜单主键，按钮标识）集合</param>
    /// <returns>过滤后的菜单元数据</returns>
    public static JsonElement Filter(
        string metaJson
        , long menuId
        , IReadOnlySet<(long MenuId, string AuthMark)> granted
    ) {
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(metaJson) ? "{}" : metaJson)!.AsObject();
        if (node["authList"] is JsonArray list) {
            var kept = list
                .Where(x => x?["authMark"]?.GetValue<string>() is { } mark && granted.Contains((menuId, mark)))
                .Select(x => x!.DeepClone())
                .ToArray();
            node["authList"] = new JsonArray(kept);
        }

        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>
    ///     读取菜单元数据中 authList 声明的按钮权限标识
    /// </summary>
    /// <param name="metaJson">菜单元数据 JSON</param>
    /// <returns>按钮权限标识集合，没有声明或格式不符时为空集合</returns>
    public static HashSet<string> ReadAuthMarks(string metaJson) {
        var marks = new HashSet<string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(metaJson) ? "{}" : metaJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("authList", out var list)
            || list.ValueKind != JsonValueKind.Array) {
            return marks;
        }

        foreach (var item in list.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("authMark", out var mark) && mark.ValueKind == JsonValueKind.String) {
                _ = marks.Add(mark.GetString()!);
            }
        }

        return marks;
    }
}