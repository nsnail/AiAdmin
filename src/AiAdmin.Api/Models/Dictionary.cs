// 定义字典目录树和字典内容实体

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
    public ICollection<DictionaryCategory> Children { get; set; } = [];

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
    public ICollection<DictionaryItem> Items { get; set; } = [];

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

/// <summary>
///     字典内容实体
/// </summary>
public sealed class DictionaryItem : EntityBase
{
    /// <summary>
    ///     所属目录
    /// </summary>
    public DictionaryCategory Category { get; init; } = null!;

    /// <summary>
    ///     所属目录主键
    /// </summary>
    public long CategoryId { get; set; }

    /// <summary>
    ///     内容主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     字典标签
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    ///     排序值
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    ///     字典键
    /// </summary>
    public required string Value { get; set; }
}