using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     计划作业执行记录，保存完整的请求和响应内容
/// </summary>
public sealed class ScheduledJobExecution : EntityBase
{
    /// <summary>
    ///     错误信息
    /// </summary>
    [ListFilter("scheduledJob.executionFields.errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    ///     结束执行时间
    /// </summary>
    [ListFilter("scheduledJob.executionFields.finishedAt", "date")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    ///     执行记录主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     请求体
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    ///     请求头
    /// </summary>
    public string RequestHeaders { get; set; } = "{}";

    /// <summary>
    ///     请求方法
    /// </summary>
    [ListFilter(
        "scheduledJob.executionFields.requestMethod", "select", Options = ["GET:GET", "POST:POST", "PUT:PUT", "PATCH:PATCH", "DELETE:DELETE"]
    )]
    public string RequestMethod { get; init; } = string.Empty;

    /// <summary>
    ///     请求地址
    /// </summary>
    [ListFilter("scheduledJob.executionFields.requestUrl")]
    public string RequestUrl { get; init; } = string.Empty;

    /// <summary>
    ///     响应体
    /// </summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>
    ///     响应头
    /// </summary>
    public string ResponseHeaders { get; set; } = "{}";

    /// <summary>
    ///     响应状态码
    /// </summary>
    [ListFilter("scheduledJob.executionFields.responseStatusCode", "number")]
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    ///     所属作业
    /// </summary>
    public ScheduledJob ScheduledJob { get; init; } = null!;

    /// <summary>
    ///     作业主键
    /// </summary>
    public long ScheduledJobId { get; init; }

    /// <summary>
    ///     开始执行时间
    /// </summary>
    [ListFilter("scheduledJob.executionFields.startedAt", "date")]
    public DateTime StartedAt { get; init; }

    /// <summary>
    ///     执行结果状态
    /// </summary>
    [ListFilter(
        "scheduledJob.executionFields.status", "select"
        , Options =
        [
            "0:scheduledJob.status.waiting", "1:scheduledJob.status.running", "2:scheduledJob.status.success", "3:scheduledJob.status.failed"
            , "4:scheduledJob.status.timeout"
        ]
    )]
    public ScheduledJobStatus Status { get; set; } = ScheduledJobStatus.Running;
}