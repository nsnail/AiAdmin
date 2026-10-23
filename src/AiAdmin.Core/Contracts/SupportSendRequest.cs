using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     发送在线客服消息请求，用户端不需要指定会话，客服端必须指定会话
/// </summary>
public sealed class SupportSendRequest
{
    /// <summary>
    ///     消息正文，纯文本
    /// </summary>
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    ///     会话主键，客服回复时必填
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? ConversationId { get; init; }
}