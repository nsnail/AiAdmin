// 定义字典目录树和字典内容实体
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

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
    public long CategoryId { get; init; }

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