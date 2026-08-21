using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

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