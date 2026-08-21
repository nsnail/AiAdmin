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
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}