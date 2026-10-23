namespace AiAdmin.Api.Contracts;

/// <summary>
///     客服会话已读状态实时推送载荷，用于多个页面之间同步未读数
/// </summary>
/// <param name="ConversationId">会话主键</param>
/// <param name="OwnerUserId">会话所属用户主键</param>
/// <param name="UserUnreadCount">会话当前用户未读数量</param>
/// <param name="ServiceUnreadCount">会话当前客服未读数量</param>
public sealed record RealtimeSupportReadResult(long ConversationId, long OwnerUserId, int UserUnreadCount, int ServiceUnreadCount);