// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录请求
/// </summary>
/// <param name="UserName">登录用户名</param>
/// <param name="Password">登录密码</param>
public sealed record LoginRequest(string UserName, string Password);

/// <summary>
///     登录令牌响应
/// </summary>
public sealed record LoginResult(string Token, string RefreshToken);

/// <summary>
///     当前用户信息响应
/// </summary>
public sealed record CurrentUserResult(long UserId, string UserName, string Email, string? Avatar, string[] Roles, string[] Buttons);