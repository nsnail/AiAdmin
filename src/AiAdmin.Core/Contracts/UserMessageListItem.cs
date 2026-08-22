namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户消息列表项
/// </summary>
public sealed record UserMessageListItem(long Id, string Title, string Content, DateTimeOffset CreatedAt, bool IsRead);