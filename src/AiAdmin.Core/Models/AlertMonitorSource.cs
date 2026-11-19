namespace AiAdmin.Api.Models;

/// <summary>
///     告警监控的数据来源
/// </summary>
public enum AlertMonitorSource
{
    /// <summary>
    ///     字典内容，按字典目录编码和字典标签定位
    /// </summary>
    Dictionary = 1

    ,

    /// <summary>
    ///     Redis 缓存，按缓存键定位，仅支持字符串类型
    /// </summary>
    Redis = 2
}