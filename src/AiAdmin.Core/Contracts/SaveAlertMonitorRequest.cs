using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     告警监控保存请求，新增和修改共用
/// </summary>
public sealed class SaveAlertMonitorRequest
{
    /// <summary>
    ///     字符串比较是否区分大小写，默认不区分
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    ///     字典目录编码，数据来源为字典时必填
    /// </summary>
    [MaxLength(100)]
    public string CategoryCode { get; init; } = string.Empty;

    /// <summary>
    ///     监控主键，修改时必填
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     报警时提醒的 Telegram 用户名，多个以空格、逗号或分号分隔
    /// </summary>
    [MaxLength(500)]
    public string Mentions { get; init; } = string.Empty;

    /// <summary>
    ///     比较的指标：1 为值本身，2 为长度，长度仅 Redis 来源可用，默认值本身
    /// </summary>
    public AlertMonitorMetric Metric { get; init; } = AlertMonitorMetric.Value;

    /// <summary>
    ///     监控名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     比较操作符
    /// </summary>
    [JsonRequired]
    public AlertMonitorOperator Operator { get; init; }

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    public string Remark { get; init; } = string.Empty;

    /// <summary>
    ///     数据来源
    /// </summary>
    [JsonRequired]
    public AlertMonitorSource Source { get; init; }

    /// <summary>
    ///     被监控的字典标签或 Redis 缓存键
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Target { get; init; } = string.Empty;

    /// <summary>
    ///     阈值，数字比较时必须是数字
    /// </summary>
    [MaxLength(1000)]
    public string Threshold { get; init; } = string.Empty;

    /// <summary>
    ///     比较类型
    /// </summary>
    [JsonRequired]
    public AlertMonitorValueType ValueType { get; init; }
}