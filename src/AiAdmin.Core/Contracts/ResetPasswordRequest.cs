using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     重置密码请求
/// </summary>
/// <param name="Email">用户电子邮箱</param>
/// <param name="VerificationCode">邮箱验证码</param>
/// <param name="Password">新密码</param>
public sealed record ResetPasswordRequest(
    string Email
    , string VerificationCode
    , [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")] string Password);