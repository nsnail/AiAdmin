namespace AiAdmin.Api.Contracts;

/// <summary>
///     系统消息列表项
/// </summary>
/// <param name="Id">消息主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="UpdatedAt">更新时间</param>
/// <param name="Title">消息标题</param>
/// <param name="Content">消息正文</param>
/// <param name="IsPopup">是否弹窗提醒</param>
/// <param name="RecipientCount">收件人数</param>
public sealed record SystemMessageListItem(
    long Id
    , DateTimeOffset CreatedAt
    , DateTimeOffset? UpdatedAt
    , string Title
    , string Content
    , bool IsPopup
    , int RecipientCount);