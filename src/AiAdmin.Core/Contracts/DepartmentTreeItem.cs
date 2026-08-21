namespace AiAdmin.Api.Contracts;

/// <summary>
///     部门树节点
/// </summary>
public sealed class DepartmentTreeItem
{
    /// <summary>
    ///     子部门节点集合
    /// </summary>
    public IReadOnlyList<DepartmentTreeItem> Children { get; init; } = [];

    /// <summary>
    ///     部门编码
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    ///     创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    ///     部门邮箱
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    ///     部门主键
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    ///     是否启用部门
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    ///     部门负责人
    /// </summary>
    public string Leader { get; init; } = string.Empty;

    /// <summary>
    ///     部门名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     父部门主键
    /// </summary>
    public long? ParentId { get; init; }

    /// <summary>
    ///     部门电话
    /// </summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    ///     同级显示顺序
    /// </summary>
    public int Sort { get; init; }
}