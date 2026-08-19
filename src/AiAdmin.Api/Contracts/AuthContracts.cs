// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录请求
/// </summary>
/// <param name="UserName">登录用户名</param>
/// <param name="Password">登录密码</param>
/// <param name="Challenge">算力挑战</param>
/// <param name="Proof">算力证明</param>
public sealed record LoginRequest(string UserName, string Password, string Challenge, string Proof);

/// <summary>
///     登录算力挑战
/// </summary>
public sealed record LoginChallengeResult(string Challenge, int Difficulty);

/// <summary>
///     登录令牌响应
/// </summary>
public sealed record LoginResult(string Token, string RefreshToken);

/// <summary>
///     登录页面配置
/// </summary>
public sealed record LoginConfigResult(bool LoginSliderVerification);

/// <summary>
///     当前用户信息响应
/// </summary>
public sealed record CurrentUserResult(
    long UserId
    , string UserName
    , string Email
    , string Phone
    , string Gender
    , string? Avatar
    , string[] Roles
    , string[] Buttons);