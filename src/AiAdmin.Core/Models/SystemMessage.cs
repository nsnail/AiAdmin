using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     管理员发布的系统消息
/// </summary>
public sealed class SystemMessage : EntityBase
{
    /// <summary>消息正文 HTML</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息主键</summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>是否在用户端自动弹出提醒</summary>
    public bool IsPopup { get; set; }

    /// <summary>用户收件关联集合</summary>
    public ICollection<UserMessage> Recipients { get; init; } = [];

    /// <summary>发送人主键</summary>
    public long SenderId { get; set; }

    /// <summary>消息标题</summary>
    [ListFilter("messageManagement.title")]
    public string Title { get; set; } = string.Empty;
}