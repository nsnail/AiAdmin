namespace AiAdmin.Api.Contracts;

/// <summary>
///     客服消息实时推送载荷
/// </summary>
/// <param name="Message">新消息</param>
/// <param name="OwnerUserId">会话所属用户主键，前端据此区分是自己的会话还是客服收到的其他用户会话</param>
/// <param name="UserName">会话所属用户名，客服端用于更新会话列表</param>
/// <param name="ServiceUnreadCount">会话当前客服未读数量</param>
/// <param name="UserUnreadCount">会话当前用户未读数量</param>
public sealed record RealtimeSupportMessageResult(
    SupportMessageItem Message
    , long OwnerUserId
    , string UserName
    , int ServiceUnreadCount
    , int UserUnreadCount);