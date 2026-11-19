using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     告警监控实体：监控一个字典内容或 Redis 缓存键的值，命中条件时触发报警，恢复后解除报警，通知发送到系统设置中的报警机器人
/// </summary>
public sealed class AlertMonitor : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     当前是否处于报警状态，由扫描作业维护，用于避免重复报警
    /// </summary>
    public bool AlertActive { get; set; }

    /// <summary>
    ///     最近一次进入报警状态的时间
    /// </summary>
    public DateTime? AlertedAt { get; set; }

    /// <summary>
    ///     字符串比较是否区分大小写，默认不区分
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    ///     被监控的字典目录编码，仅数据来源为字典时使用
    /// </summary>
    [MaxLength(100)]
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>
    ///     监控主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用，停用后扫描作业跳过该监控，且不会发送解除通知
    /// </summary>
    [ListFilter(
        "listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], GroupCount = true
        , Span = 2, Sort = 5
    )]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     最近一次扫描时间
    /// </summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>
    ///     最近一次扫描的状态说明：正常、命中条件或取不到值的原因
    /// </summary>
    [MaxLength(500)]
    public string LastMessage { get; set; } = string.Empty;

    /// <summary>
    ///     最近一次扫描读取到的值，取不到值时为空
    /// </summary>
    [MaxLength(1000)]
    public string LastValue { get; set; } = string.Empty;

    /// <summary>
    ///     报警时提醒的 Telegram 用户名，多个以空格分隔，每项以 @ 开头
    /// </summary>
    [MaxLength(500)]
    public string Mentions { get; set; } = string.Empty;

    /// <summary>
    ///     比较的指标：值本身或长度，长度仅 Redis 来源可用
    /// </summary>
    [ListFilter(
        "alertMonitor.fields.metric", "select", Options = ["1:alertMonitor.metric.value", "2:alertMonitor.metric.length"], GroupCount = true, Span = 2
        , Sort = 3
    )]
    public AlertMonitorMetric Metric { get; set; } = AlertMonitorMetric.Value;

    /// <summary>
    ///     监控名称，用于通知文本和列表展示
    /// </summary>
    [MaxLength(100)]
    [ListFilter("alertMonitor.fields.name", Placeholder = "alertMonitor.placeholder.name", Span = 4, Sort = 0)]
    public required string Name { get; set; }

    /// <summary>
    ///     比较操作符
    /// </summary>
    [ListFilter(
        "alertMonitor.fields.operator", "select"
        , Options =
        [
            "1:alertMonitor.operator.greaterThan", "2:alertMonitor.operator.greaterThanOrEqual", "3:alertMonitor.operator.lessThan"
            , "4:alertMonitor.operator.lessThanOrEqual", "5:alertMonitor.operator.equal", "6:alertMonitor.operator.notEqual"
            , "7:alertMonitor.operator.contains", "8:alertMonitor.operator.notContains", "9:alertMonitor.operator.startsWith"
            , "10:alertMonitor.operator.endsWith"
        ], Span = 3, Sort = 4
    )]
    public AlertMonitorOperator Operator { get; set; } = AlertMonitorOperator.GreaterThan;

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    [ListFilter("alertMonitor.fields.remark", Placeholder = "alertMonitor.placeholder.remark", Span = 3, Sort = 6)]
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    ///     数据来源
    /// </summary>
    [ListFilter(
        "alertMonitor.fields.source", "select", Options = ["1:alertMonitor.source.dictionary", "2:alertMonitor.source.redis"], GroupCount = true
        , Span = 2, Sort = 2
    )]
    public AlertMonitorSource Source { get; set; } = AlertMonitorSource.Dictionary;

    /// <summary>
    ///     被监控的字典标签或 Redis 缓存键
    /// </summary>
    [MaxLength(500)]
    [ListFilter("alertMonitor.fields.target", Placeholder = "alertMonitor.placeholder.target", Span = 4, Sort = 1)]
    public required string Target { get; set; }

    /// <summary>
    ///     阈值，数字比较时必须是数字，字符串比较时是待比较的文本
    /// </summary>
    [MaxLength(1000)]
    public string Threshold { get; set; } = string.Empty;

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     比较类型
    /// </summary>
    [ListFilter(
        "alertMonitor.fields.valueType", "select", Options = ["1:alertMonitor.valueType.number", "2:alertMonitor.valueType.string"], GroupCount = true
        , Span = 2, Sort = 3
    )]
    public AlertMonitorValueType ValueType { get; set; } = AlertMonitorValueType.Number;
}