// 定义系统用户实体及其角色、部门和邀请码

using System.Security.Cryptography;
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
    public string? Avatar { get; init; }

    /// <summary>
    ///     电子邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     性别编码
    /// </summary>
    public string Gender { get; set; } = "male";

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
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     密码哈希
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    ///     联系电话
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    ///     用户部门关联集合
    /// </summary>
    public ICollection<UserDepartment> UserDepartments { get; set; } = [];

    /// <summary>
    ///     登录用户名
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}