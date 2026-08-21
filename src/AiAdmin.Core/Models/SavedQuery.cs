using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户保存的查询条件实体
/// </summary>
public sealed class SavedQuery : EntityBase
{
    /// <summary>
    ///     查询条件序列化文本
    /// </summary>
    public required string FilterJson { get; set; }

    /// <summary>
    ///     查询条件主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     查询条件名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     页面路由
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    ///     所属用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     所属用户主键
    /// </summary>
    public long UserId { get; init; }
}