using Microsoft.Extensions.Logging.Abstractions;

namespace AiAdmin.Api.Logging;

/// <summary>
///     将单个日志分类写入 Redis 队列的记录器
/// </summary>
/// <param name="categoryName">日志分类名称</param>
/// <param name="queue">Redis 日志队列</param>
/// <param name="options">日志输出配置</param>
internal sealed class ElasticsearchLogger(string categoryName, ElasticsearchLogQueue queue, ElasticsearchLogOptions options) : ILogger
{
    /// <summary>
    ///     创建日志作用域
    /// </summary>
    /// <typeparam name="TState">作用域状态类型</typeparam>
    /// <param name="state">作用域状态</param>
    /// <returns>用于结束作用域的对象</returns>
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull {
        return NullLogger.Instance.BeginScope(state);
    }

    /// <summary>
    ///     判断指定日志级别是否启用
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>启用时返回 true</returns>
    public bool IsEnabled(LogLevel logLevel) {
        return options.Enabled && logLevel != LogLevel.None;
    }

    /// <summary>
    ///     格式化日志并异步写入 Redis 队列
    /// </summary>
    /// <typeparam name="TState">日志状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">日志事件</param>
    /// <param name="state">日志状态</param>
    /// <param name="exception">日志异常</param>
    /// <param name="formatter">日志格式化方法</param>
    public void Log<TState>(
        LogLevel logLevel
        , EventId eventId
        , TState state
        , Exception? exception
        , Func<TState, Exception?, string> formatter
    ) {
        if (!IsEnabled(logLevel) || categoryName.StartsWith("AiAdmin.Api.Logging", StringComparison.Ordinal)) {
            return;
        }

        var message = formatter(state, exception);
        var fields = ReadFields(state);
        _ = EnqueueAsync(
            new ElasticsearchLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow
                , Level = logLevel
                , Category = categoryName
                , Source = fields.GetValueOrDefault("Source")?.ToString() ?? categoryName
                , LogType = fields.GetValueOrDefault("LogType")?.ToString() ?? ResolveLogType(eventId)
                , ThreadId = Environment.CurrentManagedThreadId
                , Message = message
                , Exception = exception?.ToString()
                , EventId = eventId.Id
                , EventName = eventId.Name
                , RequestMethod = GetString(fields, "RequestMethod")
                , ClientIp = GetString(fields, "ClientIp")
                , ServerIp = GetString(fields, "ServerIp")
                , UserAgent = GetString(fields, "UserAgent")
                , RequestRelativeUrl = GetString(fields, "RequestRelativeUrl")
                , RequestUrl = GetString(fields, "RequestUrl")
                , ElapsedMilliseconds = GetLong(fields, "ElapsedMilliseconds")
                , StatusCode = GetInt(fields, "StatusCode")
                , ApiResponseCode = GetInt(fields, "ApiResponseCode")
                , UserId = GetLong(fields, "UserId")
                , UserName = GetString(fields, "UserName")
                , RequestBody = GetString(fields, "RequestBody")
                , RequestHeaders = GetString(fields, "RequestHeaders")
                , RequestContentType = GetString(fields, "RequestContentType")
                , TraceId = GetLong(fields, "TraceId")
                , WorkerId = GetLong(fields, "WorkerId") ?? 0
                , ResponseHeaders = GetString(fields, "ResponseHeaders")
                , ResponseBody = GetString(fields, "ResponseBody")
                , ResponseContentType = GetString(fields, "ResponseContentType")
                , Sql = GetString(fields, "Sql")
            }
        );
    }

    /// <summary>
    ///     获取结构化整数数值字段
    /// </summary>
    /// <param name="fields">结构化字段集合</param>
    /// <param name="name">字段名称</param>
    /// <returns>字段值</returns>
    private static int? GetInt(
        IReadOnlyDictionary<string, object?> fields
        , string name
    ) {
        return int.TryParse(GetString(fields, name), out var value) ? value : null;
    }

    /// <summary>
    ///     获取结构化长整数数值字段
    /// </summary>
    /// <param name="fields">结构化字段集合</param>
    /// <param name="name">字段名称</param>
    /// <returns>字段值</returns>
    private static long? GetLong(
        IReadOnlyDictionary<string, object?> fields
        , string name
    ) {
        return long.TryParse(GetString(fields, name), out var value) ? value : null;
    }

    /// <summary>
    ///     获取结构化字符串字段
    /// </summary>
    /// <param name="fields">结构化字段集合</param>
    /// <param name="name">字段名称</param>
    /// <returns>字段值</returns>
    private static string? GetString(
        IReadOnlyDictionary<string, object?> fields
        , string name
    ) {
        return fields.TryGetValue(name, out var value) ? value?.ToString() : null;
    }

    /// <summary>
    ///     从结构化日志状态读取字段
    /// </summary>
    /// <typeparam name="TState">日志状态类型</typeparam>
    /// <param name="state">日志状态</param>
    /// <returns>结构化字段集合</returns>
    private static Dictionary<string, object?> ReadFields<TState>(TState state) {
        return state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs
                .Where(x => !string.Equals(x.Key, "{OriginalFormat}", StringComparison.Ordinal))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     根据事件编号解析日志类型
    /// </summary>
    /// <param name="eventId">日志事件编号</param>
    /// <returns>日志类型</returns>
    private static string ResolveLogType(EventId eventId) {
        return eventId.Name switch
        {
            not null when eventId.Name.StartsWith("Api", StringComparison.Ordinal) => "Api"
            , not null when eventId.Name.StartsWith("Database", StringComparison.Ordinal) => "Sql"
            , not null when eventId.Name.StartsWith("ExternalHttp", StringComparison.Ordinal) => "Http"
            , _ => "System"
        };
    }

    /// <summary>
    ///     将日志写入 Redis 队列并隔离队列故障
    /// </summary>
    /// <param name="entry">日志内容</param>
    /// <returns>异步写入任务</returns>
    private async Task EnqueueAsync(ElasticsearchLogEntry entry) {
        try {
            _ = await queue.EnqueueAsync(entry).ConfigureAwait(false);
        }
        catch {
            // 日志管道故障不能影响业务请求
        }
    }
}