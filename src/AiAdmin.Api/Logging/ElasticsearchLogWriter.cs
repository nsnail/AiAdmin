using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Services;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     调用 Elasticsearch Bulk API 按日志发生时的 UTC 日期写入每日索引
/// </summary>
/// <param name="httpClient">HTTP 客户端</param>
/// <param name="options">日志输出配置</param>
/// <param name="httpRequests">记录索引模板请求与响应的统一服务</param>
public sealed class ElasticsearchLogWriter(HttpClient httpClient, IOptions<ElasticsearchLogOptions> options, ExternalHttpRequestService httpRequests)
{
    private const int _MAX_ATTEMPTS = 3;

    private volatile bool _templateInitialized;

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
        await EnsureIndexTemplateAsync(settings, cancellationToken).ConfigureAwait(false);
        await SendWithRetryAsync(settings, BuildPayload(entries, settings.Index), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     构建 Elasticsearch Bulk API 的 NDJSON 载荷，跨天批次逐条选择日期索引
    /// </summary>
    /// <param name="entries">待写入日志</param>
    /// <param name="index">目标索引前缀</param>
    /// <returns>NDJSON 载荷</returns>
    private static string BuildPayload(
        IEnumerable<ElasticsearchLogEntry> entries
        , string index
    ) {
        var payload = new StringBuilder();
        foreach (var entry in entries) {
            var dailyIndex = $"{index}-{entry.Timestamp.UtcDateTime.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)}";
            _ = payload.Append("{\"index\":{\"_index\":").Append(JsonSerializer.Serialize(dailyIndex)).Append("}}\n");
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

        return payload.ToString();
    }

    /// <summary>
    ///     创建 Elasticsearch Bulk API 请求
    /// </summary>
    /// <param name="settings">Elasticsearch 配置</param>
    /// <param name="payload">NDJSON 载荷</param>
    /// <returns>HTTP 请求</returns>
    private static HttpRequestMessage CreateRequest(
        ElasticsearchLogOptions settings
        , string payload
    ) {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Uri.TrimEnd('/')}/_bulk")
        {
            Version = HttpVersion.Version11
            , VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            , Content = new StringContent(payload, Encoding.UTF8, "application/x-ndjson")
        };
        request.Headers.ExpectContinue = false;
        if (string.IsNullOrWhiteSpace(settings.Username)) {
            return request;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return request;
    }

    /// <summary>
    ///     首次写入前注册精确匹配字段模板，供 Elasticsearch 自动创建日志索引时应用
    /// </summary>
    /// <param name="settings">Elasticsearch 配置</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>模板注册任务</returns>
    /// <exception cref="HttpRequestException">索引模板注册失败</exception>
    private async Task EnsureIndexTemplateAsync(
        ElasticsearchLogOptions settings
        , CancellationToken cancellationToken
    ) {
        if (_templateInitialized) {
            return;
        }

        // keyword 不分词；限制字符数以避免超长 SQL 或 URL 超过 Lucene 单词字节上限
        string[] fields =
        [
            "category", "clientIp", "eventName", "level", "logType", "requestContentType", "requestMethod", "requestRelativeUrl", "requestUrl"
            , "responseContentType", "serverIp", "source", "sql", "userAgent", "userName"
        ];
        var properties = fields.ToDictionary(field => field, _ => new { type = "keyword", ignore_above = 8191 });
        var payload = JsonSerializer.Serialize(
            new { index_patterns = new[] { $"{settings.Index}-*" }, template = new { mappings = new { properties } } }
        );
        using var request = CreateRequest(settings, payload);
        request.Method = HttpMethod.Put;
        request.RequestUri = new Uri($"{settings.Uri.TrimEnd('/')}/_index_template/{Uri.EscapeDataString(settings.Index)}-logs");
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await httpRequests.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is < 200 or >= 300) {
            throw new HttpRequestException($"Elasticsearch index template failed with {response.StatusCode}: {response.ResponseBody}");
        }

        _templateInitialized = true;
    }

    /// <summary>
    ///     执行单次 Bulk API 请求并校验响应
    /// </summary>
    /// <param name="settings">Elasticsearch 配置</param>
    /// <param name="payload">NDJSON 载荷</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步发送任务</returns>
    /// <exception cref="HttpRequestException">HTTP 请求失败</exception>
    /// <exception cref="InvalidOperationException">Bulk 响应包含失败项</exception>
    private async Task SendOnceAsync(
        ElasticsearchLogOptions settings
        , string payload
        , CancellationToken cancellationToken
    ) {
        using var request = CreateRequest(settings, payload);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        if (responseBody.Contains("\"errors\":true", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("Elasticsearch bulk response contains item errors");
        }
    }

    /// <summary>
    ///     按固定退避策略重试 Bulk API 请求
    /// </summary>
    /// <param name="settings">Elasticsearch 配置</param>
    /// <param name="payload">NDJSON 载荷</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步发送任务</returns>
    private async Task SendWithRetryAsync(
        ElasticsearchLogOptions settings
        , string payload
        , CancellationToken cancellationToken
    ) {
        for (var attempt = 1; attempt <= _MAX_ATTEMPTS; ++attempt) {
            try {
                await SendOnceAsync(settings, payload, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException) when (attempt < _MAX_ATTEMPTS) {
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 250), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}