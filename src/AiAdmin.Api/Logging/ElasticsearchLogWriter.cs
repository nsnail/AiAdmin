using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<string, byte> _ensuredIndices = new();

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
        await EnsureIndicesAsync(settings, entries.Select(entry => GetDailyIndex(settings.Index, entry)).Distinct(), cancellationToken)
            .ConfigureAwait(false);
        try {
            await SendWithRetryAsync(settings, BuildPayload(entries, settings.Index), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException) {
            // 索引可能被外部删除，清空缓存以便下次重新创建
            _ensuredIndices.Clear();
            throw;
        }
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
            _ = payload.Append("{\"index\":{\"_index\":").Append(JsonSerializer.Serialize(GetDailyIndex(index, entry))).Append("}}\n");
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
    ///     计算日志所属的 UTC 每日索引名
    /// </summary>
    /// <param name="index">目标索引前缀</param>
    /// <param name="entry">日志条目</param>
    /// <returns>每日索引名</returns>
    private static string GetDailyIndex(
        string index
        , ElasticsearchLogEntry entry
    ) {
        return $"{index}-{entry.Timestamp.UtcDateTime.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    ///     读取 JSON 对象中的属性文本，不存在时返回问号
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="name">属性名称</param>
    /// <returns>属性文本或问号</returns>
    private static string ReadText(
        JsonElement element
        , string name
    ) {
        return element.TryGetProperty(name, out var value) ? value.ToString() : "?";
    }

    /// <summary>
    ///     生成单个失败项的状态、索引、错误类型与原因摘要
    /// </summary>
    /// <param name="body">Bulk 响应中带 error 的操作结果</param>
    /// <returns>失败项摘要</returns>
    private static string SummarizeFailedItem(JsonElement body) {
        var error = body.GetProperty("error");
        var status = ReadText(body, "status");
        var index = ReadText(body, "_index");
        var type = ReadText(error, "type");
        var reason = ReadText(error, "reason");
        return $"[status={status}, index={index}, type={type}, reason={reason}]";
    }

    /// <summary>
    ///     从 Bulk 响应中提取前几个失败项的状态、索引、错误类型与原因，便于排查
    /// </summary>
    /// <param name="responseBody">Bulk 响应正文</param>
    /// <returns>失败项摘要，无法解析时返回截断的原始响应</returns>
    private static string SummarizeItemErrors(string responseBody) {
        const int maxItems = 3;
        try {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("items", out var items)) {
                return Truncate(responseBody);
            }

            // 每个 item 形如 {"index":{...,"error":{...}}}
            var failed = items
                .EnumerateArray()
                .SelectMany(item => item.EnumerateObject().Select(operation => operation.Value))
                .Where(body => body.TryGetProperty("error", out _))
                .ToList();
            var summaries = failed.Take(maxItems).Select(SummarizeFailedItem).ToList();
            return $"{failed.Count} failed item(s), first {summaries.Count}: {string.Join(' ', summaries)}";
        }
        catch (JsonException) {
            return Truncate(responseBody);
        }
    }

    /// <summary>
    ///     截断过长文本，避免异常消息过大
    /// </summary>
    /// <param name="value">原始文本</param>
    /// <returns>最多 1000 个字符的文本</returns>
    private static string Truncate(string value) {
        return value.Length <= 1000 ? value : value[..1000];
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
    ///     确保每日索引已存在；集群禁用 action.auto_create_index 时 Bulk 不会自动建索引
    /// </summary>
    /// <param name="settings">Elasticsearch 配置</param>
    /// <param name="indices">待确认的索引名</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>索引创建任务</returns>
    /// <exception cref="HttpRequestException">索引创建失败</exception>
    private async Task EnsureIndicesAsync(
        ElasticsearchLogOptions settings
        , IEnumerable<string> indices
        , CancellationToken cancellationToken
    ) {
        foreach (var index in indices.Where(index => !_ensuredIndices.ContainsKey(index))) {
            using var request = CreateRequest(settings, "{}");
            request.Method = HttpMethod.Put;
            request.RequestUri = new Uri($"{settings.Uri.TrimEnd('/')}/{Uri.EscapeDataString(index)}");
            request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var response = await httpRequests.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            var created = response.StatusCode is >= 200 and < 300;
            var exists = response.StatusCode == (int)HttpStatusCode.BadRequest
                         && response.ResponseBody.Contains("resource_already_exists_exception", StringComparison.Ordinal);
            if (!created && !exists) {
                throw new HttpRequestException($"Elasticsearch index creation failed with {response.StatusCode}: {response.ResponseBody}");
            }

            _ = _ensuredIndices.TryAdd(index, 0);
        }
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
            throw new InvalidOperationException($"Elasticsearch bulk response contains item errors: {SummarizeItemErrors(responseBody)}");
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