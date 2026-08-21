namespace AiAdmin.Api.Logging;

/// <summary>
///     系统日志列表项
/// </summary>
public sealed record SystemLogItem
{
    /// <summary>
    ///     日志类型
    /// </summary>
    public string LogType { get; init; } = string.Empty;

    /// <summary>
    ///     日志分类
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    ///     日志来源
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    ///     事件编号
    /// </summary>
    public int EventId { get; init; }

    /// <summary>
    ///     事件名称
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    ///     异常信息
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    ///     日志级别
    /// </summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>
    ///     日志消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     日志产生时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    ///     线程编号
    /// </summary>
    public int ThreadId { get; init; }

    /// <summary>
    ///     HTTP 请求方法
    /// </summary>
    public string? RequestMethod { get; init; }

    /// <summary>
    ///     客户端 IP
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    ///     服务器 IP
    /// </summary>
    public string? ServerIp { get; init; }

    /// <summary>
    ///     客户端 User-Agent
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    ///     请求相对地址
    /// </summary>
    public string? RequestRelativeUrl { get; init; }

    /// <summary>
    ///     请求绝对地址
    /// </summary>
    public string? RequestUrl { get; init; }

    /// <summary>
    ///     整体耗时毫秒数
    /// </summary>
    public long? ElapsedMilliseconds { get; init; }

    /// <summary>
    ///     响应 HTTP 状态码
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    ///     API 响应业务编码
    /// </summary>
    public int? ApiResponseCode { get; init; }

    /// <summary>
    ///     用户编号
    /// </summary>
    public long? UserId { get; init; }

    /// <summary>
    ///     用户名
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    ///     请求体
    /// </summary>
    public string? RequestBody { get; init; }

    /// <summary>
    ///     请求头
    /// </summary>
    public string? RequestHeaders { get; init; }

    /// <summary>
    ///     请求内容类型
    /// </summary>
    public string? RequestContentType { get; init; }

    /// <summary>
    ///     请求编号
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    ///     响应头
    /// </summary>
    public string? ResponseHeaders { get; init; }

    /// <summary>
    ///     响应体
    /// </summary>
    public string? ResponseBody { get; init; }

    /// <summary>
    ///     响应内容类型
    /// </summary>
    public string? ResponseContentType { get; init; }

    /// <summary>
    ///     SQL 语句
    /// </summary>
    public string? Sql { get; init; }
}