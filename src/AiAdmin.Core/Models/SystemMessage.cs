using System.ComponentModel.DataAnnotations.Schema;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     管理员发布的系统消息
/// </summary>
public sealed class SystemMessage : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     消息收件人数
    /// </summary>
    [NotMapped]
    public int RecipientCount => Recipients.Count;

    /// <summary>消息正文 HTML</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息主键</summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否在用户端自动弹出提醒
    /// </summary>
    [ListFilter(
        "messageManagement.popup", "select", Span = 2, Options = ["true:listFilter.option.yes", "false:listFilter.option.no"], GroupCount = true
    )]
    public bool IsPopup { get; init; }

    /// <summary>用户收件关联集合</summary>
    public ICollection<UserMessage> Recipients { get; init; } = [];

    /// <summary>发送人主键</summary>
    public long SenderId { get; init; }

    /// <summary>
    ///     消息标题
    /// </summary>
    [ListFilter("messageManagement.title", Span = 4, Placeholder = "messageManagement.searchTitle")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}