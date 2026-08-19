using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// 定义字典管理接口的请求和响应模型
namespace AiAdmin.Api.Contracts;

/// <summary>
///     字典目录树节点
/// </summary>
public sealed record DictionaryCategoryResult(
    long Id
    , string Code
    , string Name
    , long? ParentId
    , int Sort
    , bool IsEnabled
    , IReadOnlyList<DictionaryCategoryResult> Children);

/// <summary>
///     字典内容列表项
/// </summary>
public sealed record DictionaryItemResult(long Id, long CategoryId, string Value, string Label, int Sort, bool IsEnabled, string Remark);

/// <summary>
///     字典目录保存请求
/// </summary>
public sealed class SaveDictionaryCategoryRequest
{
    /// <summary>
    ///     目录编码
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     启用状态
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     目录名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     父目录主键
    /// </summary>
    public long? ParentId { get; init; }

    /// <summary>
    ///     排序值
    /// </summary>
    [JsonRequired]
    public int Sort { get; init; }
}

/// <summary>
///     字典内容保存请求
/// </summary>
public sealed class SaveDictionaryItemRequest
{
    /// <summary>
    ///     启用状态
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     字典标签
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    ///     备注
    /// </summary>
    [StringLength(500)]
    public string Remark { get; init; } = string.Empty;

    /// <summary>
    ///     排序值
    /// </summary>
    [JsonRequired]
    public int Sort { get; init; }

    /// <summary>
    ///     字典键
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Value { get; init; } = string.Empty;
}