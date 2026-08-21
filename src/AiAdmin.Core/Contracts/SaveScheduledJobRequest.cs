namespace AiAdmin.Api.Contracts;

/// <summary>
///     计划作业保存请求
/// </summary>
public sealed class SaveScheduledJobRequest
{
    /// <summary>
    ///     Cron 表达式
    /// </summary>
    public required string CronExpression { get; set; }

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     作业名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     请求体模板
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    ///     请求头 JSON
    /// </summary>
    public string RequestHeadersJson { get; set; } = "{}";

    /// <summary>
    ///     请求方法
    /// </summary>
    public string RequestMethod { get; set; } = "GET";

    /// <summary>
    ///     请求地址
    /// </summary>
    public required string RequestUrl { get; set; }

    /// <summary>
    ///     超时秒数
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}