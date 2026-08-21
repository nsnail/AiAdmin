using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表行启用状态更新请求
/// </summary>
public sealed class UpdateEnabledRequest
{
    /// <summary>
    ///     是否启用当前记录
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; }
}