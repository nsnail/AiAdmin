using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     调用 Elasticsearch Bulk API 写入日志
/// </summary>
/// <param name="httpClient">HTTP 客户端</param>
/// <param name="options">日志输出配置</param>
public sealed class ElasticsearchLogWriter(HttpClient httpClient, IOptions<ElasticsearchLogOptions> options)
{
    /// <summary>
    ///     将日志批量写入 Elasticsearch
    /// </summary>
    /// <param name="entries">待写入日志</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步写入任务</returns>
    /// <exception cref="HttpRequestException">Elasticsearch 请求失败</exception>
    /// <exception cref="InvalidOperationException">Elasticsearch Bulk 响应包含失败项</exception>
    public async Task WriteAsync(
        IReadOnlyCollection<ElasticsearchLogEntry> entries
        , CancellationToken cancellationToken
    ) {
        if (!options.Value.Enabled || entries.Count == 0) {
            return;
        }

        var settings = options.Value;
        var payload = new StringBuilder();
        foreach (var entry in entries) {
            _ = payload.Append("{\"index\":{\"_index\":").Append(JsonSerializer.Serialize(settings.Index)).Append("}}\n");
            _ = payload
                .Append(
                    JsonSerializer.Serialize(
                        new
                        {
                            timestamp = entry.Timestamp
                            , level = entry.Level.ToString()
                            , message = entry.Message
                            , source = string.IsNullOrWhiteSpace(entry.Source) ? entry.Category : entry.Source
                            , category = entry.Category
                            , logType = entry.LogType
                            , threadId = entry.ThreadId
                            , exception = entry.Exception
                            , eventId = entry.EventId
                            , eventName = entry.EventName
                            , requestMethod = entry.RequestMethod
                            , clientIp = entry.ClientIp
                            , serverIp = entry.ServerIp
                            , userAgent = entry.UserAgent
                            , requestRelativeUrl = entry.RequestRelativeUrl
                            , requestUrl = entry.RequestUrl
                            , elapsedMilliseconds = entry.ElapsedMilliseconds
                            , statusCode = entry.StatusCode
                            , apiResponseCode = entry.ApiResponseCode
                            , userId = entry.UserId
                            , userName = entry.UserName
                            , requestBody = entry.RequestBody
                            , requestHeaders = entry.RequestHeaders
                            , requestContentType = entry.RequestContentType
                            , traceId = entry.TraceId
                            , workerId = entry.WorkerId
                            , responseHeaders = entry.ResponseHeaders
                            , responseBody = entry.ResponseBody
                            , responseContentType = entry.ResponseContentType
                            , sql = entry.Sql
                        }
                    )
                )
                .Append('\n');
        }

        var payloadText = payload.ToString();
        for (var attempt = 1; attempt <= 3; ++attempt) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Uri.TrimEnd('/')}/_bulk");
                request.Version = HttpVersion.Version11;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/x-ndjson");
                request.Headers.ExpectContinue = false;
                if (!string.IsNullOrWhiteSpace(settings.Username)) {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                if (responseBody.Contains("\"errors\":true", StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("Elasticsearch bulk response contains item errors");
                }

                return;
            }
            catch (HttpRequestException) when (attempt < 3) {
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 250), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}