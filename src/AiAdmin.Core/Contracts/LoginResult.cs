namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录令牌响应
/// </summary>
public sealed record LoginResult(string Token, string RefreshToken);