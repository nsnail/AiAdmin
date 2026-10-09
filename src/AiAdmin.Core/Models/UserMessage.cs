namespace AiAdmin.Api.Models;

/// <summary>
///     用户消息收件和阅读状态
/// </summary>
public sealed class UserMessage : EntityBase
{
    /// <summary>
    ///     是否已删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    ///     是否已读
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    ///     关联消息
    /// </summary>
    public SystemMessage Message { get; init; } = null!;

    /// <summary>
    ///     消息主键
    /// </summary>
    public long MessageId { get; init; }

    /// <summary>
    ///     关联用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     用户主键
    /// </summary>
    public long UserId { get; init; }
}