using System.Security.Cryptography;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统用户实体
/// </summary>
public sealed class User : EntityBase
{
    /// <summary>
    ///     头像地址
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    ///     电子邮箱
    /// </summary>
    [ListFilter("listFilter.user.email", Placeholder = "listFilter.placeholder.email", Span = 4)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     性别编码
    /// </summary>
    [ListFilter("listFilter.user.gender", "select", Span = 2, Options = ["1:listFilter.option.male", "2:listFilter.option.female"])]
    public UserGender Gender { get; set; } = UserGender.Male;

    /// <summary>
    ///     用户主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     该用户用于邀请新用户的邀请码
    /// </summary>
    public string InvitationCode { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));

    /// <summary>
    ///     是否启用
    /// </summary>
    [ListFilter("listFilter.common.status", "select", Span = 2, Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"])]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     密码哈希
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    ///     联系电话
    /// </summary>
    [ListFilter("listFilter.user.phone", Span = 3, Placeholder = "listFilter.placeholder.phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    ///     用户部门关联集合
    /// </summary>
    public ICollection<UserDepartment> UserDepartments { get; init; } = [];

    /// <summary>
    ///     登录用户名
    /// </summary>
    [ListFilter("listFilter.user.userName", Span = 3, Placeholder = "listFilter.placeholder.userName", Sort = 0)]
    public required string UserName { get; init; }

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}