namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘总览结果，一次返回所有卡片所需数据
/// </summary>
/// <param name="Summary">核心指标汇总</param>
/// <param name="LoginTrend">登录趋势，Primary 为登录次数，Secondary 为登录用户数</param>
/// <param name="NewUserTrend">新增用户趋势，Primary 为新增用户数</param>
/// <param name="ExecutionTrend">作业执行趋势，Primary 为成功次数，Secondary 为失败次数</param>
/// <param name="DeviceDistribution">登录设备类型分布</param>
/// <param name="BrowserDistribution">登录浏览器分布</param>
/// <param name="RegionRanking">登录地区排行</param>
/// <param name="JobStatusDistribution">计划作业状态分布</param>
/// <param name="RecentLogins">最近登录记录</param>
/// <param name="RecentFailures">最近异常的作业执行记录</param>
/// <param name="GeneratedAt">数据生成时间</param>
public sealed record DashboardOverviewResult(
    DashboardSummaryResult Summary
    , IReadOnlyList<DashboardTrendPoint> LoginTrend
    , IReadOnlyList<DashboardTrendPoint> NewUserTrend
    , IReadOnlyList<DashboardTrendPoint> ExecutionTrend
    , IReadOnlyList<DashboardNameValue> DeviceDistribution
    , IReadOnlyList<DashboardNameValue> BrowserDistribution
    , IReadOnlyList<DashboardNameValue> RegionRanking
    , IReadOnlyList<DashboardNameValue> JobStatusDistribution
    , IReadOnlyList<DashboardRecentLoginResult> RecentLogins
    , IReadOnlyList<DashboardFailedExecutionResult> RecentFailures
    , DateTimeOffset GeneratedAt);