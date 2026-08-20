// 定义部门树实体及用户部门多对多关联实体。

using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统部门实体
/// </summary>
public sealed class Department : EntityBase
{
    /// <summary>
    ///     默认部门编码
    /// </summary>
    public const string DEFAULT_CODE = "DEFAULT";

    /// <summary>
    ///     默认部门数据库名称
    /// </summary>
    public const string DEFAULT_NAME = "Default Department";

    /// <summary>
    ///     子部门集合
    /// </summary>
    public ICollection<Department> Children { get; init; } = [];

    /// <summary>
    ///     部门编码
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    ///     部门邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     部门主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用部门
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     部门负责人
    /// </summary>
    public string Leader { get; set; } = string.Empty;

    /// <summary>
    ///     部门名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     父部门
    /// </summary>
    public Department? Parent { get; set; }

    /// <summary>
    ///     父部门主键
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    ///     部门电话
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    ///     同级显示顺序
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    ///     用户部门关联集合
    /// </summary>
    public ICollection<UserDepartment> UserDepartments { get; init; } = [];
}

/// <summary>
///     用户与部门关联实体
/// </summary>
public sealed class UserDepartment : EntityBase
{
    /// <summary>
    ///     关联部门
    /// </summary>
    public Department Department { get; init; } = null!;

    /// <summary>
    ///     部门主键
    /// </summary>
    public long DepartmentId { get; init; }

    /// <summary>
    ///     关联用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     用户主键
    /// </summary>
    public long UserId { get; init; }
}