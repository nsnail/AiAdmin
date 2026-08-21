namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录请求
/// </summary>
/// <param name="UserName">登录用户名</param>
/// <param name="Password">登录密码</param>
/// <param name="Challenge">算力挑战</param>
/// <param name="Proof">算力证明</param>
public sealed record LoginRequest(string UserName, string Password, string Challenge, string Proof);