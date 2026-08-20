// 定义登录和当前用户信息相关的数据传输模型。

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
///     用户注册请求
/// </summary>
/// <param name="UserName">登录用户名</param>
/// <param name="Password">登录密码</param>
/// <param name="Email">电子邮箱</param>
/// <param name="VerificationCode">邮箱验证码</param>
/// <param name="InvitationCode">邀请码</param>
public sealed record RegisterRequest(
    string UserName
    , [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    string Password
    , string Email
    , string VerificationCode
    , string? InvitationCode);

/// <summary>
///     发送注册邮箱验证码请求
/// </summary>
/// <param name="Email">电子邮箱</param>
/// <param name="PuzzleTicket">拼图校验凭证</param>
public sealed record RegisterCodeRequest(string Email, string PuzzleTicket);

/// <summary>
///     邮箱验证码拼图挑战
/// </summary>
public sealed record RegisterPuzzleResult(
    string ChallengeId
    , string BackgroundImage
    , string PieceImage
    , int Width
    , int Height
    , int PieceSize
    , int PieceY);

/// <summary>
///     拼图校验请求
/// </summary>
/// <param name="ChallengeId">挑战主键</param>
/// <param name="OffsetX">拼图横向偏移</param>
/// <param name="Email">接收验证码的电子邮箱</param>
public sealed record VerifyRegisterPuzzleRequest(
    string ChallengeId
    , [property: JsonRequired]
    int OffsetX
    , string Email);

/// <summary>
///     拼图校验结果
/// </summary>
public sealed record VerifyRegisterPuzzleResult(string PuzzleTicket);

/// <summary>
///     登录令牌响应
/// </summary>
public sealed record LoginResult(string Token, string RefreshToken);

/// <summary>
///     登录页面配置
/// </summary>
public sealed record LoginConfigResult(bool LoginSliderVerification, bool RegistrationEnabled, bool EmailVerificationEnabled);

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