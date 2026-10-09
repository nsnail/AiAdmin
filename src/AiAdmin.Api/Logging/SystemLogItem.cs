using AiAdmin.Api.Attributes;

namespace AiAdmin.Api.Logging;

/// <summary>
///     系统日志列表项
/// </summary>
public sealed record SystemLogItem
{
    /// <summary>
    ///     API 响应业务编码
    /// </summary>
    public int? ApiResponseCode { get; init; }

    /// <summary>
    ///     日志分类
    /// </summary>
    [ListFilter("systemLog.fields.category", IsVisible = false)]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    ///     客户端 IP
    /// </summary>
    [ListFilter("systemLog.fields.clientIp", IsVisible = false)]
    public string? ClientIp { get; init; }

    /// <summary>
    ///     整体耗时毫秒数
    /// </summary>
    [ListFilter("systemLog.fields.elapsedMilliseconds", IsVisible = false)]
    public long? ElapsedMilliseconds { get; init; }

    /// <summary>
    ///     事件编号
    /// </summary>
    [ListFilter("systemLog.fields.eventId", IsVisible = false)]
    public int EventId { get; init; }

    /// <summary>
    ///     事件名称
    /// </summary>
    [ListFilter("systemLog.fields.eventName", IsVisible = false)]
    public string? EventName { get; init; }

    /// <summary>
    ///     异常信息
    /// </summary>
    [ListFilter("systemLog.fields.exception", IsVisible = false)]
    public string? Exception { get; init; }

    /// <summary>
    ///     日志级别
    /// </summary>
    [ListFilter(
        "systemLog.filters.level", "select", Sort = 0, Span = 3
        , Options =
        [
            "Trace:systemLog.levels.Trace", "Debug:systemLog.levels.Debug", "Information:systemLog.levels.Information"
            , "Warning:systemLog.levels.Warning", "Error:systemLog.levels.Error", "Critical:systemLog.levels.Critical"
        ]
    )]
    public string Level { get; init; } = string.Empty;

    /// <summary>
    ///     日志类型
    /// </summary>
    [ListFilter(
        "systemLog.filters.logType", "select", Sort = 1, Span = 3
        , Options = ["System:systemLog.types.System", "Api:systemLog.types.Api", "Sql:systemLog.types.Sql", "Http:systemLog.types.Http"]
    )]
    public string LogType { get; init; } = string.Empty;

    /// <summary>
    ///     日志消息
    /// </summary>
    [ListFilter("systemLog.fields.message", Sort = 2, Span = 6)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     请求体
    /// </summary>
    [ListFilter("systemLog.fields.requestBody", IsVisible = false)]
    public string? RequestBody { get; init; }

    /// <summary>
    ///     请求内容类型
    /// </summary>
    [ListFilter("systemLog.fields.requestContentType", IsVisible = false)]
    public string? RequestContentType { get; init; }

    /// <summary>
    ///     请求头
    /// </summary>
    [ListFilter("systemLog.fields.requestHeaders", IsVisible = false)]
    public string? RequestHeaders { get; init; }

    /// <summary>
    ///     HTTP 请求方法
    /// </summary>
    [ListFilter("systemLog.fields.requestMethod", IsVisible = false)]
    public string? RequestMethod { get; init; }

    /// <summary>
    ///     请求相对地址
    /// </summary>
    [ListFilter("systemLog.fields.requestRelativeUrl", IsVisible = false)]
    public string? RequestRelativeUrl { get; init; }

    /// <summary>
    ///     请求绝对地址
    /// </summary>
    [ListFilter("systemLog.fields.requestUrl", IsVisible = false)]
    public string? RequestUrl { get; init; }

    /// <summary>
    ///     响应体
    /// </summary>
    [ListFilter("systemLog.fields.responseBody", IsVisible = false)]
    public string? ResponseBody { get; init; }

    /// <summary>
    ///     响应内容类型
    /// </summary>
    [ListFilter("systemLog.fields.responseContentType", IsVisible = false)]
    public string? ResponseContentType { get; init; }

    /// <summary>
    ///     响应头
    /// </summary>
    [ListFilter("systemLog.fields.responseHeaders", IsVisible = false)]
    public string? ResponseHeaders { get; init; }

    /// <summary>
    ///     服务器 IP
    /// </summary>
    [ListFilter("systemLog.fields.serverIp", IsVisible = false)]
    public string? ServerIp { get; init; }

    /// <summary>
    ///     日志来源
    /// </summary>
    [ListFilter("systemLog.fields.source", IsVisible = false)]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    ///     SQL 语句
    /// </summary>
    [ListFilter("systemLog.fields.sql", IsVisible = false)]
    public string? Sql { get; init; }

    /// <summary>
    ///     响应 HTTP 状态码
    /// </summary>
    [ListFilter("systemLog.fields.statusCode", IsVisible = false)]
    public int? StatusCode { get; init; }

    /// <summary>
    ///     线程编号
    /// </summary>
    [ListFilter("systemLog.fields.threadId", IsVisible = false)]
    public int ThreadId { get; init; }

    /// <summary>
    ///     日志产生时间
    /// </summary>
    [ListFilter("systemLog.filters.timestamp", "date", Sort = int.MinValue, Span = 6)]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    ///     雪花链路主键
    /// </summary>
    [ListFilter("systemLog.fields.traceId", IsVisible = false)]
    public long? TraceId { get; init; }

    /// <summary>
    ///     客户端 User-Agent
    /// </summary>
    [ListFilter("systemLog.fields.userAgent", IsVisible = false)]
    public string? UserAgent { get; init; }

    /// <summary>
    ///     用户编号
    /// </summary>
    public long? UserId { get; init; }

    /// <summary>
    ///     用户名
    /// </summary>
    [ListFilter("systemLog.fields.userName", IsVisible = false)]
    public string? UserName { get; init; }

    /// <summary>
    ///     Snowflake WorkerId
    /// </summary>
    public long WorkerId { get; init; }
}