using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理数据库审计日志请求
/// </summary>
public sealed class CleanupDatabaseAuditLogsRequest
{
    /// <summary>
    ///     审计日志保留小时数
    /// </summary>
    [Range(1, 87600)]
    public int Hours { get; init; } = 72;
}