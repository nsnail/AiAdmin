namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘核心指标汇总
/// </summary>
/// <param name="TotalUsers">用户总数</param>
/// <param name="EnabledUsers">启用用户数</param>
/// <param name="NewUsersToday">今日新增用户数</param>
/// <param name="NewUsersInRange">统计区间新增用户数</param>
/// <param name="LoginsToday">今日登录次数</param>
/// <param name="ActiveUsersToday">今日登录用户数</param>
/// <param name="TotalJobs">计划作业总数</param>
/// <param name="EnabledJobs">启用的计划作业数</param>
/// <param name="RunningJobs">运行中的计划作业数</param>
/// <param name="FailedJobs">最近一次执行失败或超时的计划作业数</param>
/// <param name="ExecutionsToday">今日执行次数</param>
/// <param name="ExecutionSuccessRate">统计区间执行成功率，范围 0 到 100</param>
/// <param name="TotalRoles">角色总数</param>
/// <param name="TotalDepartments">部门总数</param>
/// <param name="AvailableBalance">钱包可用余额合计</param>
/// <param name="FrozenBalance">钱包冻结余额合计</param>
public sealed record DashboardSummaryResult(
    int TotalUsers
    , int EnabledUsers
    , int NewUsersToday
    , int NewUsersInRange
    , int LoginsToday
    , int ActiveUsersToday
    , int TotalJobs
    , int EnabledJobs
    , int RunningJobs
    , int FailedJobs
    , int ExecutionsToday
    , decimal ExecutionSuccessRate
    , int TotalRoles
    , int TotalDepartments
    , decimal AvailableBalance
    , decimal FrozenBalance);