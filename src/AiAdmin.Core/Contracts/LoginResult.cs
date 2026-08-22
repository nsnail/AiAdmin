namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录令牌响应
/// </summary>
/// <param name="Token">访问令牌</param>
/// <param name="RefreshToken">刷新令牌</param>
/// <param name="PreviousLogin">上次登录摘要，首次登录时为空</param>
public sealed record LoginResult(string Token, string RefreshToken, PreviousLoginResult? PreviousLogin);