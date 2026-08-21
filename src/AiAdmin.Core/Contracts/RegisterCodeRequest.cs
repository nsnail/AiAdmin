namespace AiAdmin.Api.Contracts;

/// <summary>
///     发送注册邮箱验证码请求
/// </summary>
/// <param name="Email">电子邮箱</param>
/// <param name="PuzzleTicket">拼图校验凭证</param>
public sealed record RegisterCodeRequest(string Email, string PuzzleTicket);