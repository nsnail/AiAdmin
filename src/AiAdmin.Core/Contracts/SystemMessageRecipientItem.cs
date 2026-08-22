namespace AiAdmin.Api.Contracts;

/// <summary>
///     系统消息收件人状态明细
/// </summary>
public sealed record SystemMessageRecipientItem(
    long UserId
    , string UserName
    , string UserEmail
    , bool IsRead
    , bool IsDeleted
);