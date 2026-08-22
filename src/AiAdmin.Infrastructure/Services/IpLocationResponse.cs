using System.Text.Json.Serialization;

namespace AiAdmin.Api.Services;

/// <summary>
///     IP 归属地接口响应项
/// </summary>
public sealed class IpLocationResponse
{
    /// <summary>
    ///     响应状态码
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>
    ///     IP 地址
    /// </summary>
    [JsonPropertyName("ip")]
    public string Ip { get; init; } = string.Empty;

    /// <summary>
    ///     IP 归属地区
    /// </summary>
    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;
}