using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiAdmin.Api.Logging;

namespace AiAdmin.Api.Services;

/// <summary>
///     统一执行并记录外部 HTTP 请求
/// </summary>
/// <param name="httpClientFactory">HTTP 客户端工厂</param>
/// <param name="httpContextAccessor">当前 HTTP 请求上下文访问器</param>
/// <param name="logger">外部请求日志记录器</param>
public sealed partial class ExternalHttpRequestService(
    IHttpClientFactory httpClientFactory
    , IHttpContextAccessor httpContextAccessor
    , ILogger<ExternalHttpRequestService> logger)
{
    /// <summary>
    ///     日志中需要脱敏的消息头名称，避免密钥和凭据写入日志
    /// </summary>
    private static readonly HashSet<string> _sensitiveHeaders =
    [
        with(StringComparer.OrdinalIgnoreCase), "X-API-Key", "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie"
    ];

    /// <summary>
    ///     把请求地址查询串中的密钥类参数值以 *** 替代，避免只能通过地址传递的密钥写入日志
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <returns>脱敏后的请求地址</returns>
    public static string MaskUrl(string url) {
        return SensitiveQueryRegex().Replace(url, "$1=***");
    }

    /// <summary>
    ///     发送外部 HTTP 请求并返回完整快照
    /// </summary>
    /// <param name="request">待发送的 HTTP 请求</param>
    /// <param name="completionOption">响应完成模式</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>包含请求和响应详情的快照</returns>
    /// <exception cref="HttpRequestException">外部请求发送失败</exception>
    public async Task<ExternalHttpResponse> SendAsync(
        HttpRequestMessage request
        , HttpCompletionOption completionOption
        , CancellationToken cancellationToken
    ) {
        using var lease = ShutdownGate.Enter(ShutdownActivity.Outbound);
        long? traceId = null;
        if (HttpTraceContext.TryGet(httpContextAccessor.HttpContext, out var currentTraceId)) {
            traceId = currentTraceId;
            _ = request.Headers.Remove(HttpTraceContext.HEADER_NAME);
            _ = request.Headers.TryAddWithoutValidation(HttpTraceContext.HEADER_NAME, currentTraceId.ToString(CultureInfo.InvariantCulture));
        }

        var requestBody = request.Content is not null && IsTextContentType(request.Content.Headers.ContentType?.MediaType)
            ? await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : string.Empty;
        var requestHeaders = SerializeHeaders(request.Headers, request.Content?.Headers);
        var requestContentType = request.Content?.Headers.ContentType?.ToString();
        var stopwatch = Stopwatch.StartNew();

        using var response = await httpClientFactory.CreateClient().SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        var responseBody = IsTextContentType(response.Content.Headers.ContentType?.MediaType)
            ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : string.Empty;
        stopwatch.Stop();
        var responseHeaders = SerializeHeaders(response.Headers, response.Content.Headers);
        var result = new ExternalHttpResponse
        {
            RequestUrl = MaskUrl(request.RequestUri?.ToString() ?? string.Empty)
            , RequestMethod = request.Method.Method
            , RequestHeaders = requestHeaders
            , RequestBody = requestBody
            , RequestContentType = requestContentType
            , StatusCode = (int)response.StatusCode
            , ResponseHeaders = responseHeaders
            , ResponseBody = responseBody
            , ResponseContentType = response.Content.Headers.ContentType?.ToString()
            , ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        };
        if (!logger.IsEnabled(LogLevel.Information)) {
            return result;
        }

        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["LogType"] = "Http"
            , ["Source"] = "HTTP"
            , ["RequestMethod"] = result.RequestMethod
            , ["RequestUrl"] = result.RequestUrl
            , ["ElapsedMilliseconds"] = result.ElapsedMilliseconds
            , ["StatusCode"] = result.StatusCode
            , ["RequestBody"] = result.RequestBody
            , ["RequestHeaders"] = result.RequestHeaders
            , ["RequestContentType"] = result.RequestContentType
            , ["ResponseHeaders"] = result.ResponseHeaders
            , ["ResponseBody"] = result.ResponseBody
            , ["ResponseContentType"] = result.ResponseContentType
            , ["TraceId"] = traceId
        };
        var state = new StructuredLogState("External HTTP request completed", fields);
        logger.Log(
            LogLevel.Information, new EventId(3202, "ExternalHttpRequestCompleted"), state, null, static (
                value
                , _
            ) => value.Message
        );

        return result;
    }

    /// <summary>
    ///     判断内容类型是否适合按文本读取
    /// </summary>
    /// <param name="mediaType">HTTP 媒体类型</param>
    /// <returns>适合按文本读取时返回 true</returns>
    private static bool IsTextContentType(string? mediaType) {
        return !string.IsNullOrWhiteSpace(mediaType)
               && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     匹配查询串中的 apiKey、api_key、key、token、access_token、secret、password 参数
    /// </summary>
    /// <returns>查询参数匹配正则</returns>
    [GeneratedRegex("(?<=[?&])(apiKey|api_key|key|token|access_token|secret|password)=[^&#]*", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveQueryRegex();

    /// <summary>
    ///     将请求或响应头序列化为 JSON，密钥和凭据类消息头的值以 *** 替代
    /// </summary>
    /// <param name="headers">HTTP 消息头</param>
    /// <param name="contentHeaders">HTTP 内容头</param>
    /// <returns>序列化后的请求头</returns>
    private static string SerializeHeaders(
        HttpHeaders headers
        , HttpContentHeaders? contentHeaders
    ) {
        var values = headers.ToDictionary(
            item => item.Key, item => _sensitiveHeaders.Contains(item.Key) ? "***" : string.Join(",", item.Value), StringComparer.OrdinalIgnoreCase
        );
        if (contentHeaders is null) {
            return JsonSerializer.Serialize(values);
        }

        foreach (var item in contentHeaders) {
            values[item.Key] = string.Join(",", item.Value);
        }

        return JsonSerializer.Serialize(values);
    }
}