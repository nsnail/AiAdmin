using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     计划作业实体，描述一个按 Cron 表达式执行的 HTTP 请求
/// </summary>
public sealed class ScheduledJob : EntityBase
{
    /// <summary>
    ///     Cron 触发表达式
    /// </summary>
    [ListFilter("scheduledJob.fields.cronExpression", Placeholder = "scheduledJob.placeholder.cronExpression")]
    public required string CronExpression { get; set; }

    /// <summary>
    ///     作业执行记录集合
    /// </summary>
    public ICollection<ScheduledJobExecution> Executions { get; init; } = [];

    /// <summary>
    ///     作业主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用
    /// </summary>
    [ListFilter("listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"])]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     最近一次错误信息
    /// </summary>
    [ListFilter("scheduledJob.fields.lastError")]
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    ///     最近一次完成时间
    /// </summary>
    [ListFilter("scheduledJob.fields.lastFinishedAt", "date")]
    public DateTime? LastFinishedAt { get; set; }

    /// <summary>
    ///     最近一次触发时间
    /// </summary>
    [ListFilter("scheduledJob.fields.lastTriggeredAt", "date")]
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    ///     作业名称
    /// </summary>
    [ListFilter("scheduledJob.fields.name", Placeholder = "scheduledJob.placeholder.name", Sort = 0)]
    public required string Name { get; set; }

    /// <summary>
    ///     请求体模板
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    ///     请求头模板 JSON
    /// </summary>
    public string RequestHeadersJson { get; set; } = "{}";

    /// <summary>
    ///     请求方法
    /// </summary>
    [ListFilter("scheduledJob.fields.requestMethod", "select", Options = ["GET:GET", "POST:POST", "PUT:PUT", "PATCH:PATCH", "DELETE:DELETE"])]
    public string RequestMethod { get; set; } = "GET";

    /// <summary>
    ///     请求地址模板
    /// </summary>
    [ListFilter("scheduledJob.fields.requestUrl", Placeholder = "scheduledJob.placeholder.requestUrl")]
    public required string RequestUrl { get; set; }

    /// <summary>
    ///     当前执行状态
    /// </summary>
    [ListFilter(
        "scheduledJob.fields.status", "select"
        , Options =
        [
            "0:scheduledJob.status.waiting", "1:scheduledJob.status.running", "2:scheduledJob.status.success", "3:scheduledJob.status.failed"
            , "4:scheduledJob.status.timeout"
        ]
    )]
    public ScheduledJobStatus Status { get; set; } = ScheduledJobStatus.Waiting;

    /// <summary>
    ///     超时时间（秒）
    /// </summary>
    [ListFilter("scheduledJob.fields.timeoutSeconds", "number")]
    public int TimeoutSeconds { get; set; } = 30;
}