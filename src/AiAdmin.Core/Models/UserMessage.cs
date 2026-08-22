using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户消息收件和阅读状态
/// </summary>
public sealed class UserMessage
{
    /// <summary>是否已读</summary>
    public bool IsRead { get; set; }

    /// <summary>消息主键</summary>
    public long MessageId { get; set; }

    /// <summary>关联消息</summary>
    public SystemMessage Message { get; set; } = null!;

    /// <summary>是否已删除</summary>
    public bool IsDeleted { get; set; }

    /// <summary>用户主键</summary>
    public long UserId { get; set; }

    /// <summary>关联用户</summary>
    public User User { get; set; } = null!;
}