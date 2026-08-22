namespace AiAdmin.Api.Contracts;

/// <summary>
///     忘记密码验证码请求
/// </summary>
/// <param name="Email">用户电子邮箱</param>
public sealed record ForgotPasswordCodeRequest(string Email);