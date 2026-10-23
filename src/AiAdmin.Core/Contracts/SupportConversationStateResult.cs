namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户自己的客服会话状态，用于顶部聊天浮层
/// </summary>
/// <param name="ConversationId">会话主键，用户还没有发过消息时为空</param>
/// <param name="UserUnreadCount">用户未读的客服消息数量</param>
public sealed record SupportConversationStateResult(long? ConversationId, int UserUnreadCount);