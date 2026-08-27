using Microsoft.Extensions.Logging.Abstractions;

namespace AiAdmin.Api.Logging;

/// <summary>
///     仅处理错误及严重错误级别的文件日志记录器
/// </summary>
/// <param name="categoryName">日志分类名称</param>
/// <param name="options">文件日志配置</param>
/// <param name="directory">日志目录绝对路径</param>
/// <param name="syncRoot">跨分类写入同步对象</param>
internal sealed class FileLogger(string categoryName, FileLogOptions options, string directory, object syncRoot) : ILogger
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
    ///     判断指定日志级别是否写入文件
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>符合配置时返回 true</returns>
    public bool IsEnabled(LogLevel logLevel) {
        return options.Enabled && logLevel is LogLevel.Error or LogLevel.Critical;
    }

    /// <summary>
    ///     格式化并写入错误日志
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
        if (!IsEnabled(logLevel)) {
            return;
        }

        var message = formatter(state, exception);
        var eventName = string.IsNullOrWhiteSpace(eventId.Name) ? string.Empty : $" ({eventId.Name})";
        var eventText = eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name) ? string.Empty : $" EventId={eventId.Id}{eventName}";
        var exceptionText = exception is null ? string.Empty : Environment.NewLine + exception;
        var line
            = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {categoryName}{eventText}: {message}{exceptionText}{Environment.NewLine}";

        try {
            lock (syncRoot) {
                _ = Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"{options.FileName}-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(path, line);
            }
        }
        catch {
            // 文件日志故障不能影响业务请求，也不能再次通过 ILogger 记录
        }
    }
}