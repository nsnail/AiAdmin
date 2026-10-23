namespace AiAdmin.Api.Contracts;

/// <summary>
///     实时推送消息信封，前端按 Type 分发处理
/// </summary>
/// <param name="Type">消息类型，如 system-info、unread</param>
/// <param name="Data">消息载荷</param>
public sealed record RealtimeEnvelope(string Type, object? Data);