namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户消息列表项
/// </summary>
/// <param name="Id">消息标识</param>
/// <param name="Title">消息标题</param>
/// <param name="Content">消息内容</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="IsRead">是否已读</param>
/// <param name="SenderName">发送人名称</param>
/// <param name="SenderAvatar">发送人头像</param>
/// <param name="IsPopup">是否弹窗</param>
public sealed record UserMessageListItem(
    long Id
    , string Title
    , string Content
    , DateTimeOffset CreatedAt
    , bool IsRead
    , string SenderName
    , string? SenderAvatar
    , bool IsPopup);