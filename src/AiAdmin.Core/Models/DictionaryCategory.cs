using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     字典目录实体
/// </summary>
public sealed class DictionaryCategory : EntityBase
{
    /// <summary>
    ///     子目录集合
    /// </summary>
    public ICollection<DictionaryCategory> Children { get; init; } = [];

    /// <summary>
    ///     目录编码
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    ///     目录主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     字典内容集合
    /// </summary>
    public ICollection<DictionaryItem> Items { get; init; } = [];

    /// <summary>
    ///     目录名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     父目录主键，根目录为 null
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    ///     排序值
    /// </summary>
    public int Sort { get; set; }
}