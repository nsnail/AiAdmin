namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选分组选项
/// </summary>
/// <param name="Value">分组字段值</param>
/// <param name="Label">选项显示名称的客户端多语言键</param>
/// <param name="Count">当前查询条件下的数据量</param>
public sealed record ListFilterGroupOptionResult(object? Value, string Label, int Count);