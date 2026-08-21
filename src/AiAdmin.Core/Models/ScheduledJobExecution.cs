using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     计划作业执行记录，保存完整的请求和响应内容
/// </summary>
public sealed class ScheduledJobExecution : EntityBase
{
    /// <summary>错误信息</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>结束执行时间</summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>执行记录主键</summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>请求体</summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>请求头</summary>
    public string RequestHeaders { get; set; } = "{}";

    /// <summary>请求方法</summary>
    public string RequestMethod { get; set; } = string.Empty;

    /// <summary>请求地址</summary>
    public string RequestUrl { get; set; } = string.Empty;

    /// <summary>响应体</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>响应头</summary>
    public string ResponseHeaders { get; set; } = "{}";

    /// <summary>响应状态码</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>所属作业</summary>
    public ScheduledJob ScheduledJob { get; init; } = null!;

    /// <summary>作业主键</summary>
    public long ScheduledJobId { get; init; }

    /// <summary>开始执行时间</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>执行结果状态</summary>
    public ScheduledJobStatus Status { get; set; } = ScheduledJobStatus.Running;
}