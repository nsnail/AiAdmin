namespace AiAdmin.Api.Contracts;

/// <summary>
///     系统消息列表项
/// </summary>
public sealed record SystemMessageListItem(long Id, string Title, string Content, DateTimeOffset CreatedAt, int RecipientCount);