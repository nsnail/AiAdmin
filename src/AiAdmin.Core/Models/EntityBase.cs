using AiAdmin.Api.Attributes;

namespace AiAdmin.Api.Models;

/// <summary>
///     数据库实体基类
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    ///     创建时间
    /// </summary>
    [ListFilter("listFilter.common.createdAt", "date", Span = 7, Sort = int.MinValue)]
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}