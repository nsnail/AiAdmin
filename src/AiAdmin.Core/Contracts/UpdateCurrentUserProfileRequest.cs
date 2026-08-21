using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户资料更新请求
/// </summary>
public sealed class UpdateCurrentUserProfileRequest
{
    /// <summary>
    ///     电子邮箱
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    ///     性别编码
    /// </summary>
    [JsonRequired]
    public UserGender Gender { get; init; }

    /// <summary>
    ///     联系电话
    /// </summary>
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;
}