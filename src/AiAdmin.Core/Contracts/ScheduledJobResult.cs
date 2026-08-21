using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     计划作业列表项
/// </summary>
/// <param name="Id">作业编号</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="Name">作业名称</param>
/// <param name="CronExpression">Cron 表达式</param>
/// <param name="RequestUrl">请求地址</param>
/// <param name="RequestMethod">请求方法</param>
/// <param name="RequestHeadersJson">请求头 JSON</param>
/// <param name="RequestBody">请求体</param>
/// <param name="TimeoutSeconds">超时秒数</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="Status">作业状态</param>
/// <param name="LastTriggeredAt">最后触发时间</param>
/// <param name="LastFinishedAt">最后完成时间</param>
/// <param name="LastError">最后一次错误信息</param>
public sealed record ScheduledJobResult(
    long Id
    , DateTimeOffset CreatedAt
    , string Name
    , string CronExpression
    , string RequestUrl
    , string RequestMethod
    , string RequestHeadersJson
    , string RequestBody
    , int TimeoutSeconds
    , bool IsEnabled
    , ScheduledJobStatus Status
    , DateTimeOffset? LastTriggeredAt
    , DateTimeOffset? LastFinishedAt
    , string LastError);