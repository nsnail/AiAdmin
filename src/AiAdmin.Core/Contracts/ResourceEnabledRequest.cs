using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     指定资源的启用状态更新请求
/// </summary>
public sealed class ResourceEnabledRequest
{
    /// <summary>
    ///     资源主键
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; }

    /// <summary>
    ///     字符串类型的资源主键，如设备编号
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    ///     资源类型
    /// </summary>
    [Required]
    public string Resource { get; init; } = string.Empty;
}