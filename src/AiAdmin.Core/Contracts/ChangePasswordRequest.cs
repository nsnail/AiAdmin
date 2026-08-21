using System.ComponentModel.DataAnnotations;

// 定义用户查询和用户维护请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户修改密码请求
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    ///     当前密码
    /// </summary>
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>
    ///     新密码
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; init; } = string.Empty;
}