// 定义后端模型反射生成的列表筛选字段元数据

namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选可选项响应
/// </summary>
/// <param name="Label">选项显示名称</param>
/// <param name="Value">选项值</param>
public sealed record ListFilterOptionResult(string Label, string Value);