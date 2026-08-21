using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiAdmin.Api.Models;

// 定义用户查询和用户维护请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户新增或修改请求
/// </summary>
public sealed class SaveUserRequest
{
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
    ///     登录密码
    /// </summary>
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    public string? Password { get; init; }

    /// <summary>
    ///     联系电话
    /// </summary>
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    ///     角色编码集合
    /// </summary>
    [MinLength(1)]
    public string[] Roles { get; init; } = [];

    /// <summary>
    ///     登录用户名
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; init; } = string.Empty;
}