namespace AiAdmin.Api.Models;

/// <summary>
///     告警监控按什么类型比较被监控的值
/// </summary>
public enum AlertMonitorValueType
{
    /// <summary>
    ///     按数字比较，被监控的值和阈值都必须能解析为数字
    /// </summary>
    Number = 1

    ,

    /// <summary>
    ///     按字符串比较
    /// </summary>
    Text = 2
}