using System.Net;
using System.Text;

namespace AiAdmin.Tests;

/// <summary>
///     测试用 HTTP 消息处理器，返回固定响应并记录收到的请求
/// </summary>
/// <param name="statusCode">固定返回的状态码</param>
/// <param name="body">固定返回的响应体</param>
internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
{
    /// <summary>
    ///     收到的 X-API-Key 请求头值
    /// </summary>
    public string? ApiKey { get; private set; }

    /// <summary>
    ///     收到的请求体文本
    /// </summary>
    public string RequestBody { get; private set; } = string.Empty;

    /// <summary>
    ///     收到的请求地址
    /// </summary>
    public Uri? RequestUri { get; private set; }

    /// <summary>
    ///     记录请求并返回固定响应
    /// </summary>
    /// <param name="request">收到的请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>固定响应</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request
        , CancellationToken cancellationToken
    ) {
        RequestUri = request.RequestUri;
        RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ApiKey = request.Headers.TryGetValues("X-API-Key", out var values) ? values.FirstOrDefault() : null;
        return new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}