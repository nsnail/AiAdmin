using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     递归移除 JSON 对象中值为 null 或空字符串的属性
/// </summary>
public sealed class ResponseJsonCleanupMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Initializes a new instance of the response JSON cleanup middleware
    /// </summary>
    /// <summary>
    ///     执行请求并清理 JSON 响应中的空字段
    /// </summary>
    /// <param name="context">当前 HTTP 上下文</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(HttpContext context) {
        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try {
            await next(context).ConfigureAwait(false);
            if (!IsJsonResponse(context.Response) || responseBody.Length == 0) {
                responseBody.Position = 0;
                await responseBody.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            responseBody.Position = 0;
            var cleanedJson = await CleanJsonAsync(responseBody, context.RequestAborted).ConfigureAwait(false);
            context.Response.ContentLength = cleanedJson.Length;
            await originalBody.WriteAsync(cleanedJson, context.RequestAborted).ConfigureAwait(false);
        }
        finally {
            context.Response.Body = originalBody;
        }
    }

    /// <summary>
    ///     读取并清理 JSON 内容
    /// </summary>
    /// <param name="body">响应内容流</param>
    /// <param name="cancellationToken">请求取消令牌</param>
    /// <returns>清理后的 UTF-8 JSON 字节</returns>
    private static async Task<byte[]> CleanJsonAsync(
        Stream body
        , CancellationToken cancellationToken
    ) {
        var node = await JsonNode.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        RemoveEmptyProperties(node);
        return JsonSerializer.SerializeToUtf8Bytes(node, _jsonOptions);
    }

    /// <summary>
    ///     判断 JSON 节点是否为空字符串
    /// </summary>
    /// <param name="node">JSON 节点</param>
    /// <returns>是否为空字符串</returns>
    private static bool IsEmptyString(JsonNode node) {
        return node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length == 0;
    }

    /// <summary>
    ///     判断响应是否为 JSON 内容
    /// </summary>
    /// <param name="response">HTTP 响应</param>
    /// <returns>是否为 JSON 响应</returns>
    private static bool IsJsonResponse(HttpResponse response) {
        return response.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true
               || response.ContentType?.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    ///     递归移除对象中的 null 和空字符串属性
    /// </summary>
    /// <param name="node">待清理 JSON 节点</param>
    private static void RemoveEmptyProperties(JsonNode? node) {
        switch (node) {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToList()) {
                    if (property.Value is null || IsEmptyString(property.Value)) {
                        _ = jsonObject.Remove(property.Key);
                        continue;
                    }

                    RemoveEmptyProperties(property.Value);
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray) {
                    RemoveEmptyProperties(item);
                }

                break;
        }
    }
}