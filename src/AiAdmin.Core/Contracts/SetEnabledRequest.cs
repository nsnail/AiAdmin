using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     数字主键资源的启用状态更新请求
/// </summary>
public sealed class SetEnabledRequest
{
    /// <summary>
    ///     资源主键
    /// </summary>
    [Range(1, long.MaxValue)]
    public long Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; }
}