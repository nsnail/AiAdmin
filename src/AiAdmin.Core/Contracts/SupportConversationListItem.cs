namespace AiAdmin.Api.Contracts;

/// <summary>
///     在线客服会话列表项
/// </summary>
/// <param name="Id">会话主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="UserId">用户主键</param>
/// <param name="UserName">用户名</param>
/// <param name="UserAvatar">用户头像</param>
/// <param name="LastMessagePreview">最后一条消息摘要</param>
/// <param name="LastMessageAt">最后一条消息时间</param>
/// <param name="ServiceUnreadCount">客服未读数量</param>
/// <param name="IsClosed">是否已处理完毕</param>
public sealed record SupportConversationListItem(
    long Id
    , DateTimeOffset CreatedAt
    , long UserId
    , string UserName
    , string? UserAvatar
    , string LastMessagePreview
    , DateTimeOffset LastMessageAt
    , int ServiceUnreadCount
    , bool IsClosed);