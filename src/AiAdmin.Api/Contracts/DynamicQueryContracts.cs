// 定义列表接口通用的动态筛选和分页请求模型。

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
    ///     子条件的连接逻辑，支持 And 或 Or
    /// </summary>
    public string Logic { get; init; } = "And";

    /// <summary>
    ///     操作符名称
    /// </summary>
    public string? Operator { get; init; }

    /// <summary>
    ///     子筛选条件
    /// </summary>
    public List<DynamicFilter> Filters { get; init; } = [];

    /// <summary>
    ///     可嵌套的筛选节点包装，兼容 FreeSql DynamicFilterInfo 的请求格式
    /// </summary>
    [JsonPropertyName("dynamicFilter")]
    public DynamicFilter? NestedDynamicFilter { get; init; }

    /// <summary>
    ///     筛选值
    /// </summary>
    [SuppressMessage("Sonar", "S6964:Value type property used as input in a controller action should be nullable", Justification = "分组节点无需筛选值，筛选值缺失时由动态筛选服务按操作符校验")]
    public JsonElement Value { get; init; }
}

/// <summary>
///     列表动态查询请求
/// </summary>
public sealed class DynamicQueryRequest
{
    /// <summary>
    ///     当前页码
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Current { get; init; } = 1;

    /// <summary>
    ///     动态筛选根节点
    /// </summary>
    public DynamicFilter? DynamicFilter { get; init; }

    /// <summary>
    ///     每页记录数
    /// </summary>
    [Range(1, 100)]
    public int Size { get; init; } = 20;
}