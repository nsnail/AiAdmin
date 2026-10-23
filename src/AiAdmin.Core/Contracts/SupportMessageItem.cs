namespace AiAdmin.Api.Contracts;

/// <summary>
///     在线客服会话消息
/// </summary>
/// <param name="Id">消息主键</param>
/// <param name="ConversationId">会话主键</param>
/// <param name="Content">消息正文</param>
/// <param name="IsFromService">是否由客服发出</param>
/// <param name="SenderId">发送人用户主键</param>
/// <param name="SenderName">发送人用户名</param>
/// <param name="CreatedAt">发送时间</param>
public sealed record SupportMessageItem(
    long Id
    , long ConversationId
    , string Content
    , bool IsFromService
    , long SenderId
    , string SenderName
    , DateTimeOffset CreatedAt);