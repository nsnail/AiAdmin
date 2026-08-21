namespace AiAdmin.Api.Services;

/// <summary>
///     外部 HTTP 请求的完整响应快照
/// </summary>
public sealed class ExternalHttpResponse
{
    /// <summary>
    ///     请求耗时毫秒数
    /// </summary>
    public required long ElapsedMilliseconds { get; init; }

    /// <summary>
    ///     请求体
    /// </summary>
    public required string RequestBody { get; init; }

    /// <summary>
    ///     请求内容类型
    /// </summary>
    public string? RequestContentType { get; init; }

    /// <summary>
    ///     请求头 JSON
    /// </summary>
    public required string RequestHeaders { get; init; }

    /// <summary>
    ///     请求方法
    /// </summary>
    public required string RequestMethod { get; init; }

    /// <summary>
    ///     请求地址
    /// </summary>
    public required string RequestUrl { get; init; }

    /// <summary>
    ///     响应体
    /// </summary>
    public required string ResponseBody { get; init; }

    /// <summary>
    ///     响应内容类型
    /// </summary>
    public string? ResponseContentType { get; init; }

    /// <summary>
    ///     响应头 JSON
    /// </summary>
    public required string ResponseHeaders { get; init; }

    /// <summary>
    ///     响应状态码
    /// </summary>
    public required int StatusCode { get; init; }
}