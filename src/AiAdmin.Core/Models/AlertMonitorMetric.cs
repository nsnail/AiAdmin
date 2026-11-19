namespace AiAdmin.Api.Models;

/// <summary>
///     告警监控取被监控对象的哪个指标来比较
/// </summary>
public enum AlertMonitorMetric
{
    /// <summary>
    ///     值本身，字典内容或 Redis 字符串的值
    /// </summary>
    Value = 1

    ,

    /// <summary>
    ///     长度，仅 Redis 来源可用：字符串为字节数，List、Set、Hash、SortedSet、Stream 为元素个数；按数字比较，键不存在视为长度 0
    /// </summary>
    Length = 2
}