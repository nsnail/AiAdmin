namespace AiAdmin.Api.Contracts;

/// <summary>
///     钱包列表项
/// </summary>
/// <param name="UserId">钱包用户主键</param>
/// <param name="Currency">货币单位</param>
/// <param name="AvailableBalance">可用余额</param>
/// <param name="FrozenBalance">冻结金额</param>
/// <param name="TotalIncome">总收入</param>
/// <param name="TotalExpense">总支出</param>
/// <param name="LastTransactionAt">最后交易时间</param>
/// <param name="Version">并发版本号</param>
/// <param name="Id">钱包记录主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="UserName">用户名</param>
/// <param name="UserEmail">用户邮箱</param>
/// <param name="UserAvatar">用户头像</param>
public sealed record WalletListItem(
    long UserId
    , string Currency
    , decimal AvailableBalance
    , decimal FrozenBalance
    , decimal TotalIncome
    , decimal TotalExpense
    , DateTimeOffset? LastTransactionAt
    , int Version
    , long Id
    , DateTimeOffset CreatedAt
    , string UserName
    , string UserEmail
    , string? UserAvatar);