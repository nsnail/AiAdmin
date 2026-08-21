namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选可选项响应
/// </summary>
/// <param name="Label">选项显示名称</param>
/// <param name="Value">选项值</param>
public sealed record ListFilterOptionResult(string Label, string Value);