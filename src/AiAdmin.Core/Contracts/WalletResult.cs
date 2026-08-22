namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户钱包信息
/// </summary>
/// <param name="UserId">用户主键</param>
/// <param name="Currency">货币单位</param>
/// <param name="AvailableBalance">可用余额</param>
/// <param name="FrozenBalance">冻结金额</param>
/// <param name="TotalIncome">总收入</param>
/// <param name="TotalExpense">总支出</param>
/// <param name="LastTransactionAt">最后交易时间</param>
/// <param name="Version">并发版本号</param>
public sealed record WalletResult(
    long UserId
    , string Currency
    , decimal AvailableBalance
    , decimal FrozenBalance
    , decimal TotalIncome
    , decimal TotalExpense
    , DateTimeOffset? LastTransactionAt
    , int Version);