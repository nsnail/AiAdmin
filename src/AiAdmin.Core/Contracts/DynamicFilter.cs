using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     动态筛选节点
/// </summary>
public sealed class DynamicFilter
{
    /// <summary>
    ///     字段路径，支持实体的单值导航属性路径
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    ///     子筛选条件
    /// </summary>
    public List<DynamicFilter> Filters { get; init; } = [];

    /// <summary>
    ///     子条件的连接逻辑，支持 And 或 Or
    /// </summary>
    public string Logic { get; init; } = "And";

    /// <summary>
    ///     可嵌套的筛选节点包装，兼容 FreeSql DynamicFilterInfo 的请求格式
    /// </summary>
    [JsonPropertyName("dynamicFilter")]
    public DynamicFilter? NestedDynamicFilter { get; init; }

    /// <summary>
    ///     操作符名称
    /// </summary>
    public string? Operator { get; init; }

    /// <summary>
    ///     筛选值
    /// </summary>
    public JsonElement? Value { get; init; }
}