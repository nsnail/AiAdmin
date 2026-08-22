using System.Net;
using System.Text.Json;

namespace AiAdmin.Api.Services;

/// <summary>
///     查询客户端 IP 归属地区
/// </summary>
/// <param name="httpClient">IP 归属地 HTTP 客户端</param>
/// <param name="logger">日志记录器</param>
public sealed class IpLocationService(HttpClient httpClient, ILogger<IpLocationService> logger)
{
    private static readonly Action<ILogger, string, Exception?> _lookupFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2101, "IpLocationLookupFailed"), "IP location lookup failed for {ClientIp}"
    );

    /// <summary>
    ///     查询指定公网 IP 的归属地区，查询失败时返回空文本
    /// </summary>
    /// <param name="ipAddress">待查询的 IP 地址</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>IP 归属地区</returns>
    public async Task<string> GetRegionAsync(string ipAddress, CancellationToken cancellationToken = default) {
        if (!IPAddress.TryParse(ipAddress, out var address) || IPAddress.IsLoopback(address)) {
            return string.Empty;
        }

        try {
            await using var stream = await httpClient.GetStreamAsync($"?ip={Uri.EscapeDataString(ipAddress)}", cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<IpLocationResponse[]>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return result?.FirstOrDefault(x => x.Code == 0)?.Region?.Trim() ?? string.Empty;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException) {
            _lookupFailed(logger, ipAddress, exception);
            return string.Empty;
        }
    }
}