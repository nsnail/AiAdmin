namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户消息分页结果
/// </summary>
public sealed record UserMessagePageResult(IReadOnlyList<UserMessageListItem> Items, bool HasMore, int UnreadCount);