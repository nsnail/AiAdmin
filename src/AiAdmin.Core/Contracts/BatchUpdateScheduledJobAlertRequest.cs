using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     批量设置计划作业报警请求，四项报警配置整体覆盖到所选作业
/// </summary>
public sealed class BatchUpdateScheduledJobAlertRequest
{
    /// <summary>
    ///     是否启用报警
    /// </summary>
    public bool AlertEnabled { get; init; }

    /// <summary>
    ///     报警时提醒的 Telegram 用户名，多个以空格、逗号或分号分隔
    /// </summary>
    [MaxLength(500)]
    public string AlertMentions { get; init; } = string.Empty;

    /// <summary>
    ///     执行失败或超时时是否报警
    /// </summary>
    public bool AlertOnFailure { get; init; } = true;

    /// <summary>
    ///     执行耗时超过该秒数时报警，0 表示不按耗时报警
    /// </summary>
    [Range(0, 86400)]
    public int AlertOverSeconds { get; init; }

    /// <summary>
    ///     响应体匹配是否区分大小写
    /// </summary>
    public bool AlertResultCaseSensitive { get; init; }

    /// <summary>
    ///     响应体的比较操作符，为空表示不按执行结果报警
    /// </summary>
    public AlertMonitorOperator? AlertResultOperator { get; init; }

    /// <summary>
    ///     响应体匹配的阈值
    /// </summary>
    [MaxLength(500)]
    public string AlertResultValue { get; init; } = string.Empty;

    /// <summary>
    ///     响应体的比较类型
    /// </summary>
    public AlertMonitorValueType AlertResultValueType { get; init; } = AlertMonitorValueType.Text;

    /// <summary>
    ///     计划作业编号集合
    /// </summary>
    [MinLength(1)]
    public required long[] Ids { get; init; }
}