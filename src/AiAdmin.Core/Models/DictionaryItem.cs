using AiAdmin.Api.Attributes;
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
    [ListFilter("listFilter.dictionary.isEnabled", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], Sort = 3)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     字典标签
    /// </summary>
    [ListFilter("listFilter.dictionary.label", Placeholder = "listFilter.placeholder.dictionaryLabel", Sort = 0)]
    public required string Label { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    [ListFilter("listFilter.dictionary.remark", Placeholder = "listFilter.placeholder.dictionaryRemark", Sort = 4)]
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    ///     排序值
    /// </summary>
    [ListFilter("listFilter.dictionary.sort", "number", Sort = 2)]
    public int Sort { get; set; }

    /// <summary>
    ///     字典键
    /// </summary>
    [ListFilter("listFilter.dictionary.value", Placeholder = "listFilter.placeholder.dictionaryValue", Sort = 1)]
    public required string Value { get; set; }
}