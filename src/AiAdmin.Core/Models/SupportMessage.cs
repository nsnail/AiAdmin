using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     在线客服会话中的一条消息，由用户或客服发出
/// </summary>
public sealed class SupportMessage : EntityBase
{
    /// <summary>
    ///     消息正文，纯文本
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    ///     所属会话导航属性
    /// </summary>
    public SupportConversation Conversation { get; init; } = null!;

    /// <summary>
    ///     所属会话主键
    /// </summary>
    public long ConversationId { get; init; }

    /// <summary>
    ///     消息主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否由客服发出，否则为用户发出
    /// </summary>
    public bool IsFromService { get; init; }

    /// <summary>
    ///     发送人用户主键，用户发言时为会话所属用户，客服发言时为回复的客服管理员
    /// </summary>
    public long SenderId { get; init; }
}