namespace AiAdmin.Api.Contracts;

/// <summary>
///     站内信未读数推送载荷
/// </summary>
/// <param name="Count">当前用户未读站内信数量</param>
/// <param name="HasNew">本次推送是否由新消息触发</param>
public sealed record RealtimeUnreadResult(int Count, bool HasNew);