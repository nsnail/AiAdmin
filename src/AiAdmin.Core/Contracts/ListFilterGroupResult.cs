namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选字段分组统计结果
/// </summary>
/// <param name="Field">实体属性名称</param>
/// <param name="Label">字段显示名称的客户端多语言键</param>
/// <param name="ValueType">字段值类型</param>
/// <param name="Total">排除本字段筛选后的数据总量</param>
/// <param name="Options">分组选项集合</param>
public sealed record ListFilterGroupResult(
    string Field
    , string Label
    , string ValueType
    , int Total
    , IReadOnlyList<ListFilterGroupOptionResult> Options);