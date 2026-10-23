namespace AiAdmin.Api.Contracts;

/// <summary>
///     跨实例实时推送总线消息，由发布实例广播，所有订阅实例收到后只推送给各自的本地连接
/// </summary>
/// <param name="Kind">消息类型：unread、support-message、support-read</param>
/// <param name="UserIds">unread 类型的目标用户标识集合</param>
/// <param name="OwnerUserId">客服类消息所属会话的用户标识</param>
/// <param name="HasNew">unread 类型是否由新消息触发</param>
/// <param name="Message">support-message 类型的消息载荷</param>
/// <param name="Read">support-read 类型的已读载荷</param>
public sealed record RealtimeBusMessage(
    string Kind
    , long[] UserIds
    , long OwnerUserId
    , bool HasNew
    , RealtimeSupportMessageResult? Message
    , RealtimeSupportReadResult? Read);