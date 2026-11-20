namespace AiAdmin.Api.Services;

/// <summary>
///     通过校验的续签令牌内容
/// </summary>
/// <param name="UserId">用户主键</param>
/// <param name="SessionId">签名会话标识</param>
/// <param name="Fingerprint">签发时的用户身份指纹</param>
/// <param name="SessionStart">会话首次登录时间</param>
/// <param name="ExpiresAt">续签令牌到期时间</param>
/// <param name="TokenId">续签令牌唯一标识，用于保证只能使用一次</param>
public sealed record RefreshTicket(
    long UserId
    , string SessionId
    , string Fingerprint
    , DateTimeOffset SessionStart
    , DateTimeOffset ExpiresAt
    , string TokenId);