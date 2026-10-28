namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘操作日志统计结果：今日与昨日按小时的访问量对比，以及今日各接口的请求占比
/// </summary>
/// <param name="HourlyTrend">按本地小时（0 到 23）的访问量，Primary 为今日，Secondary 为昨日，今日尚未到达的小时记 0</param>
/// <param name="ApiDistribution">今日按接口统计的请求数，只列出请求最多的若干个，其余合并为路径和说明都为空的一项，路径和说明由接口表还原</param>
/// <param name="TodayTotal">今日总请求数</param>
/// <param name="YesterdayTotal">昨日总请求数</param>
/// <param name="GeneratedAt">统计生成时间</param>
public sealed record DashboardRequestStatsResult(
    IReadOnlyList<DashboardTrendPoint> HourlyTrend
    , IReadOnlyList<DashboardApiShare> ApiDistribution
    , int TodayTotal
    , int YesterdayTotal
    , DateTimeOffset GeneratedAt);