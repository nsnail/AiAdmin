using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     在线客服会话，每个用户对应唯一一个会话，客服管理员在会话中回复用户
/// </summary>
public sealed class SupportConversation : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     会话消息集合
    /// </summary>
    public ICollection<SupportMessage> Messages { get; init; } = [];

    /// <summary>
    ///     会话主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     客服是否已处理完毕，用户再次发言时自动重新打开
    /// </summary>
    [ListFilter(
        "supportChat.closed", "select", Span = 2, Options = ["true:listFilter.option.yes", "false:listFilter.option.no"], GroupCount = true
    )]
    public bool IsClosed { get; set; }

    /// <summary>
    ///     最后一条消息的摘要，用于会话列表展示
    /// </summary>
    public string LastMessagePreview { get; set; } = string.Empty;

    /// <summary>
    ///     最后一条消息的时间
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     客服尚未阅读的用户消息数量
    /// </summary>
    [ListFilter("supportChat.serviceUnread", "number", Span = 3)]
    public int ServiceUnreadCount { get; set; }

    /// <summary>
    ///     用户尚未阅读的客服消息数量
    /// </summary>
    public int UserUnreadCount { get; set; }

    /// <summary>
    ///     会话所属用户导航属性
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     会话所属用户主键
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}