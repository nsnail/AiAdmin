using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AiAdmin.Api.Logging;

namespace AiAdmin.Api.Services;

/// <summary>
///     统一执行并记录外部 HTTP 请求
/// </summary>
/// <param name="httpClientFactory">HTTP 客户端工厂</param>
/// <param name="logger">外部请求日志记录器</param>
public sealed class ExternalHttpRequestService(IHttpClientFactory httpClientFactory, ILogger<ExternalHttpRequestService> logger)
{
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
            RequestUrl = request.RequestUri?.ToString() ?? string.Empty
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

    private static bool IsTextContentType(string? mediaType) {
        return !string.IsNullOrWhiteSpace(mediaType)
               && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
    }

    private static string SerializeHeaders(
        HttpHeaders headers
        , HttpContentHeaders? contentHeaders
    ) {
        var values = headers.ToDictionary(item => item.Key, item => string.Join(",", item.Value), StringComparer.OrdinalIgnoreCase);
        if (contentHeaders is null) {
            return JsonSerializer.Serialize(values);
        }

        foreach (var item in contentHeaders) {
            values[item.Key] = string.Join(",", item.Value);
        }

        return JsonSerializer.Serialize(values);
    }
}