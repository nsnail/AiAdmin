using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户修改请求
/// </summary>
public sealed class UpdateUserRequest
{
    /// <summary>
    ///     用户头像地址，空字符串表示清空头像
    /// </summary>
    public string? Avatar { get; init; }

    /// <summary>
    ///     所属部门主键集合
    /// </summary>
    public long[] DepartmentIds { get; init; } = [];

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
    ///     是否启用用户
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     新登录密码，为空时保留原密码
    /// </summary>
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    public string? Password { get; init; }

    /// <summary>
    ///     联系电话
    /// </summary>
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    ///     是否清空用户头像地址
    /// </summary>
    public bool? RemoveAvatar { get; init; }

    /// <summary>
    ///     角色编码集合
    /// </summary>
    [MinLength(1)]
    public string[] Roles { get; init; } = [];
}