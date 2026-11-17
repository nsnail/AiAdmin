namespace AiAdmin.Api.Data;

/// <summary>
///     数据库命令审计日志配置，用于控制哪些 SQL 会被写入日志管道
/// </summary>
public sealed class DatabaseAuditOptions
{
    /// <summary>
    ///     慢 SQL 阈值（毫秒），执行耗时达到该值的命令才记录审计日志；设为 0 记录全部命令
    /// </summary>
    public int SlowThresholdMilliseconds { get; init; } = 100;
}