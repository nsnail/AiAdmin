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
    , string UserPhone
    , string UserEmail
    , string[] UserRoles
    , long[] DepartmentIds
    , string[] DepartmentNames
    , string CreateBy
    , DateTimeOffset CreateTime
    , string UpdateBy
    , DateTimeOffset? UpdateTime);

/// <summary>
///     当前用户邀请关系查询结果
/// </summary>
public sealed class ReferralTreeResult
{
    /// <summary>
    ///     当前用户的直接下级及其后代
    /// </summary>
    public IReadOnlyList<ReferralTreeItem> Children { get; init; } = [];

    /// <summary>
    ///     当前用户的邀请码
    /// </summary>
    public required string InvitationCode { get; init; }
}

/// <summary>
///     邀请关系树节点
/// </summary>
public sealed class ReferralTreeItem
{
    /// <summary>
    ///     直接下级集合
    /// </summary>
    public IReadOnlyList<ReferralTreeItem> Children { get; init; } = [];

    /// <summary>
    ///     注册时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     电子邮箱
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    ///     用户主键
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    ///     该用户继续邀请其他用户时使用的邀请码
    /// </summary>
    public required string InvitationCode { get; init; }

    /// <summary>
    ///     登录用户名
    /// </summary>
    public required string UserName { get; init; }
}

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
    [StringLength(10)]
    public string Gender { get; init; } = "male";

    /// <summary>
    ///     联系电话
    /// </summary>
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;
}

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
    [StringLength(10)]
    public string Gender { get; init; } = "male";

    /// <summary>
    ///     是否启用用户
    /// </summary>
    public bool IsEnabled { get; init; } = true;

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