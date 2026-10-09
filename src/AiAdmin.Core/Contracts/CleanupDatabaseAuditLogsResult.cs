namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理数据库审计日志结果
/// </summary>
/// <param name="DeletedCount">删除日志数量</param>
/// <param name="Cutoff">日志保留截止时间</param>
public sealed record CleanupDatabaseAuditLogsResult(int DeletedCount, DateTimeOffset Cutoff);