namespace AiAdmin.Api.Contracts;

/// <summary>
///     计划作业保存请求
/// </summary>
public sealed class SaveScheduledJobRequest
{
    /// <summary>
    ///     是否启用报警
    /// </summary>
    public bool AlertEnabled { get; set; }

    /// <summary>
    ///     报警时提醒的 Telegram 用户名，多个以空格、逗号或分号分隔
    /// </summary>
    public string AlertMentions { get; set; } = string.Empty;

    /// <summary>
    ///     执行失败或超时时是否报警
    /// </summary>
    public bool AlertOnFailure { get; set; } = true;

    /// <summary>
    ///     执行耗时超过该秒数时报警，0 表示不按耗时报警
    /// </summary>
    public int AlertOverSeconds { get; set; }

    /// <summary>
    ///     Cron 表达式
    /// </summary>
    public required string CronExpression { get; set; }

    /// <summary>
    ///     作业主键，修改时必填
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     作业名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    public string Remark { get; set; } = string.Empty;

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