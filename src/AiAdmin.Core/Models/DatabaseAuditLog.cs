// 定义数据库读写审计日志实体
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     数据库写入审计日志
/// </summary>
public sealed class DatabaseAuditLog : EntityBase
{
    /// <summary>
    ///     操作用户主键，系统任务为 null
    /// </summary>
    public long? ActorUserId { get; init; }

    /// <summary>
    ///     实体主键文本
    /// </summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    ///     实体类型名称
    /// </summary>
    public string EntityName { get; init; } = string.Empty;

    /// <summary>
    ///     审计日志主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     请求方法
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    ///     数据库操作类型
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    ///     请求路径
    /// </summary>
    public string Path { get; init; } = string.Empty;
}