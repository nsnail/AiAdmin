// 定义登录和当前用户信息相关的数据传输模型。

using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

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