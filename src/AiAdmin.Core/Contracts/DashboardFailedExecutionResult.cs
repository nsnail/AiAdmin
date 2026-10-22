using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘最近异常的计划作业执行记录
/// </summary>
/// <param name="Id">执行记录编号</param>
/// <param name="ScheduledJobId">计划作业编号</param>
/// <param name="JobName">计划作业名称</param>
/// <param name="Status">执行状态</param>
/// <param name="ErrorMessage">错误信息</param>
/// <param name="StartedAt">开始时间</param>
public sealed record DashboardFailedExecutionResult(
    long Id
    , long ScheduledJobId
    , string JobName
    , ScheduledJobStatus Status
    , string ErrorMessage
    , DateTimeOffset StartedAt);