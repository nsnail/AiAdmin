namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户消息分页结果
/// </summary>
/// <param name="Items">消息列表</param>
/// <param name="HasMore">是否还有更多记录</param>
/// <param name="UnreadCount">未读数量</param>
public sealed record UserMessagePageResult(IReadOnlyList<UserMessageListItem> Items, bool HasMore, int UnreadCount);