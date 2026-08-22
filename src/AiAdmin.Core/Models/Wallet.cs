using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户钱包实体，每个用户仅允许拥有一个钱包
/// </summary>
public sealed class Wallet : EntityBase, IOwner, IVersion
{
    /// <summary>
    ///     冻结金额
    /// </summary>
    public decimal FrozenBalance { get; set; }

    /// <summary>
    ///     钱包用户主键，同时作为钱包主键
    /// </summary>
    [ListFilter("wallet.user", "user-select", Span = 6)]
    public long UserId { get; set; }

    /// <summary>
    ///     关联用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    ///     所有者部门主键
    /// </summary>
    public long OwnerDepartmentId { get; set; }

    /// <summary>
    ///     所有者用户主键
    /// </summary>
    public long OwnerId {
        get => UserId;
        set => UserId = value;
    }

    /// <summary>
    ///     最后交易时间
    /// </summary>
    public DateTime? LastTransactionAt { get; set; }

    /// <summary>
    ///     可用余额
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    ///     总收入
    /// </summary>
    public decimal TotalIncome { get; set; }

    /// <summary>
    ///     总支出
    /// </summary>
    public decimal TotalExpense { get; set; }

    /// <summary>
    ///     并发版本号
    /// </summary>
    public int Version { get; set; } = 1;
}
