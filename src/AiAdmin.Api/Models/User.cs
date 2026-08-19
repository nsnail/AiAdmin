// 定义系统用户实体及其角色关联集合。

namespace AiAdmin.Api.Models;

/// <summary>
///     系统用户实体
/// </summary>
public sealed class User
{
    /// <summary>
    ///     头像地址
    /// </summary>
    public string? Avatar { get; init; }

    /// <summary>
    ///     创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

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
    public long Id { get; init; }

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
    ///     最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     登录用户名
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    ///     用户部门关联集合
    /// </summary>
    public ICollection<UserDepartment> UserDepartments { get; set; } = [];

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}