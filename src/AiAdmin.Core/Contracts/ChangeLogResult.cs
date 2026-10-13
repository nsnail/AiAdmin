namespace AiAdmin.Api.Contracts;

/// <summary>
///     更新日志查询结果
/// </summary>
/// <param name="Content">更新日志的 Markdown 原文，资源缺失时为空字符串</param>
public sealed record ChangeLogResult(string Content);