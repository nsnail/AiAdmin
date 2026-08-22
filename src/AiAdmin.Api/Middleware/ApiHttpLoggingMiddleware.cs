using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Data;
using AiAdmin.Api.Logging;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     记录系统 API 请求和响应的完整 HTTP 快照
/// </summary>
/// <param name="next">下一个请求处理委托</param>
/// <param name="logger">HTTP 日志记录器</param>
public sealed class ApiHttpLoggingMiddleware(RequestDelegate next, ILogger<ApiHttpLoggingMiddleware> logger)
{
    /// <summary>
    ///     记录 API 请求并继续执行请求管道
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(HttpContext context) {
        context.Response.Headers["X-Worker-Id"] = SnowflakeIdGenerator.WorkerId.ToString(CultureInfo.InvariantCulture);
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var traceId = SnowflakeIdGenerator.Next();
        HttpTraceContext.Initialize(context, traceId);
        context.Request.EnableBuffering();
        var requestBody = IsTextContentType(context.Request.ContentType)
            ? await ReadBodyAsync(context.Request.Body, context.RequestAborted).ConfigureAwait(false)
            : string.Empty;
        context.Request.Body.Position = 0;
        var requestHeaders = JsonSerializer.Serialize(
            context.Request.Headers.ToDictionary(item => item.Key, item => string.Join(",", item.Value.ToArray()), StringComparer.OrdinalIgnoreCase)
        );
        var originalResponseBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;
        var stopwatch = Stopwatch.StartNew();
        try {
            await next(context).ConfigureAwait(false);
        }
        finally {
            stopwatch.Stop();
            responseBuffer.Position = 0;
            var responseBody = IsTextContentType(context.Response.ContentType)
                ? await ReadBodyAsync(responseBuffer, CancellationToken.None).ConfigureAwait(false)
                : string.Empty;
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted).ConfigureAwait(false);
            context.Response.Body = originalResponseBody;
            var responseHeaders = JsonSerializer.Serialize(
                context.Response.Headers.ToDictionary(
                    item => item.Key, item => string.Join(",", item.Value.ToArray()), StringComparer.OrdinalIgnoreCase
                )
            );
            var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = long.TryParse(userIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUserId)
                ? parsedUserId
                : (long?)null;
            var relativeUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            if (logger.IsEnabled(LogLevel.Information)) {
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LogType"] = "Api"
                    , ["Source"] = "API"
                    , ["RequestMethod"] = context.Request.Method
                    , ["ClientIp"] = context.Connection.RemoteIpAddress?.ToString()
                    , ["ServerIp"] = context.Connection.LocalIpAddress?.ToString()
                    , ["UserAgent"] = context.Request.Headers.UserAgent.ToString()
                    , ["RequestRelativeUrl"] = relativeUrl
                    , ["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds
                    , ["StatusCode"] = context.Response.StatusCode
                    , ["ApiResponseCode"] = TryGetBusinessCode(responseBody)
                    , ["UserId"] = userId
                    , ["UserName"] = context.User.FindFirstValue(ClaimTypes.Name)
                    , ["RequestBody"] = requestBody
                    , ["RequestHeaders"] = requestHeaders
                    , ["RequestContentType"] = context.Request.ContentType
                    , ["TraceId"] = traceId
                    , ["WorkerId"] = SnowflakeIdGenerator.WorkerId
                    , ["ResponseHeaders"] = responseHeaders
                    , ["ResponseBody"] = responseBody
                    , ["ResponseContentType"] = context.Response.ContentType
                };
                var state = new StructuredLogState("API request completed", fields);
                logger.Log(
                    LogLevel.Information, new EventId(3301, "ApiHttpRequestCompleted"), state, null, static (
                        value
                        , _
                    ) => value.Message
                );
            }
        }
    }

    /// <summary>
    ///     判断内容类型是否适合按文本读取
    /// </summary>
    /// <param name="contentType">HTTP 内容类型</param>
    /// <returns>适合按文本读取时返回 true</returns>
    private static bool IsTextContentType(string? contentType) {
        if (string.IsNullOrWhiteSpace(contentType)) {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
               || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
               || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
               || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
               || mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     从当前位置读取流中的全部文本
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>读取到的文本</returns>
    private static async Task<string> ReadBodyAsync(
        Stream stream
        , CancellationToken cancellationToken
    ) {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     从 JSON 响应中读取业务码
    /// </summary>
    /// <param name="responseBody">响应体</param>
    /// <returns>业务码</returns>
    private static int? TryGetBusinessCode(string responseBody) {
        if (string.IsNullOrWhiteSpace(responseBody)) {
            return null;
        }

        try {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("code", out var code)) {
                return null;
            }

            switch (code.ValueKind) {
                case JsonValueKind.Number when code.TryGetInt32(out var number):
                    return number;
                default:
                    var parseOk = int.TryParse(code.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text);
                    return parseOk ? text : null;
            }
        }
        catch (JsonException) {
            return null;
        }
    }
}