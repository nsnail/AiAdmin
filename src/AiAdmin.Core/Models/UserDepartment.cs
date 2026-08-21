namespace AiAdmin.Api.Models;

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