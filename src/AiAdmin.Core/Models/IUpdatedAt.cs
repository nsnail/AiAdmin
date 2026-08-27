namespace AiAdmin.Api.Models;

/// <summary>
///     定义支持更新并记录最后更新时间的实体
/// </summary>
public interface IUpdatedAt
{
    /// <summary>
    ///     最后更新时间
    /// </summary>
    DateTime? UpdatedAt { get; set; }
}