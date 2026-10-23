namespace AiAdmin.Api.Contracts;

/// <summary>
///     最近一次版本更新内容的分组，如 Features、Bug Fixes
/// </summary>
/// <param name="Title">分组标题</param>
/// <param name="Items">分组下的更新条目</param>
public sealed record DashboardReleaseNoteSection(string Title, IReadOnlyList<string> Items);