namespace AiAdmin.Api.Contracts;

/// <summary>
///     系统消息收件人状态明细
/// </summary>
/// <param name="UserId">用户标识</param>
/// <param name="UserName">用户名</param>
/// <param name="UserEmail">用户邮箱</param>
/// <param name="IsRead">是否已读</param>
/// <param name="IsDeleted">是否已删除</param>
public sealed record SystemMessageRecipientItem(long UserId, string UserName, string UserEmail, bool IsRead, bool IsDeleted);