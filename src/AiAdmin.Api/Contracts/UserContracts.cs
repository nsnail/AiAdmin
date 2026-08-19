using System.ComponentModel.DataAnnotations;

// 定义用户查询和用户维护请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户列表项
/// </summary>
public sealed record UserListItem(
    long Id
    , string Avatar
    , string Status
    , string UserName
    , string UserGender
    , string NickName
    , string UserPhone
    , string UserEmail
    , string[] UserRoles
    , string CreateBy
    , DateTime CreateTime
    , string UpdateBy
    , DateTime UpdateTime);

/// <summary>
///     用户新增或修改请求
/// </summary>
public sealed class SaveUserRequest
{
    /// <summary>
    ///     电子邮箱
    /// </summary>
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    ///     性别编码
    /// </summary>
    [StringLength(10)]
    public string Gender { get; init; } = "male";

    /// <summary>
    ///     是否启用用户
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     用户昵称
    /// </summary>
    [StringLength(50)]
    public string NickName { get; init; } = string.Empty;

    /// <summary>
    ///     登录密码
    /// </summary>
    [StringLength(100, MinimumLength = 6)]
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