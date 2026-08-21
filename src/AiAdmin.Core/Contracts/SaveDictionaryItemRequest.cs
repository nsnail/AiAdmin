using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// 定义字典管理接口的请求和响应模型
namespace AiAdmin.Api.Contracts;

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