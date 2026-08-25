using AiAdmin.Api.Attributes;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户钱包实体，每个用户仅允许拥有一个钱包
/// </summary>
public sealed class Wallet : EntityBase, IOwner, IVersion
{
    /// <summary>
    ///     可用余额
    /// </summary>
    [ListFilter("wallet.availableBalance", "number", Span = 3)]
    public decimal AvailableBalance { get; init; }

    /// <summary>
    ///     冻结金额
    /// </summary>
    [ListFilter("wallet.frozenBalance", "number", Span = 3)]
    public decimal FrozenBalance { get; init; }

    /// <summary>
    ///     最后交易时间
    /// </summary>
    [ListFilter("wallet.lastTransactionAt", "date", Span = 4)]
    public DateTime? LastTransactionAt { get; init; }

    /// <summary>
    ///     所有者部门主键
    /// </summary>
    public long OwnerDepartmentId { get; set; }

    /// <summary>
    ///     所有者用户主键
    /// </summary>
    public long OwnerId
    {
        get => UserId;
        set => UserId = value;
    }

    /// <summary>
    ///     总支出
    /// </summary>
    [ListFilter("wallet.totalExpense", "number", Span = 3)]
    public decimal TotalExpense { get; init; }

    /// <summary>
    ///     总收入
    /// </summary>
    [ListFilter("wallet.totalIncome", "number", Span = 3)]
    public decimal TotalIncome { get; init; }

    /// <summary>
    ///     关联用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     钱包用户主键，同时作为钱包主键
    /// </summary>
    [ListFilter("wallet.user", "user-select", Span = 6, GroupCount = true)]
    public long UserId { get; set; }

    /// <summary>
    ///     并发版本号
    /// </summary>
    public int Version { get; set; } = 1;
}