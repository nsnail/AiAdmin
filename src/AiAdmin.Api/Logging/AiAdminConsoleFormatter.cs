using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AiAdmin.Api.Logging;

/// <summary>
///     将结构化日志状态解析为字段列表并输出到控制台
/// </summary>
internal sealed class AiAdminConsoleFormatter : ConsoleFormatter
{
    private const string _GRAY_COLOR = "\e[90m";
    private const string _GREEN_COLOR = "\e[32m";
    private const string _RED_COLOR = "\e[31m";
    private const string _RESET_COLOR = "\e[0m";
    private const string _YELLOW_COLOR = "\e[33m";

    public AiAdminConsoleFormatter()
        : base("aiadmin") {
    }

    /// <summary>
    ///     将日志条目格式化为字段列表并写入控制台
    /// </summary>
    /// <typeparam name="TState">日志状态类型</typeparam>
    /// <param name="logEntry">待格式化的日志条目</param>
    /// <param name="scopeProvider">日志作用域提供程序</param>
    /// <param name="textWriter">日志输出文本写入器</param>
    public override void Write<TState>(
        in LogEntry<TState> logEntry
        , IExternalScopeProvider? scopeProvider
        , TextWriter textWriter
    ) {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> stateFields) {
            foreach (var field in stateFields.Where(field => !string.Equals(field.Key, "{OriginalFormat}", StringComparison.Ordinal))) {
                fields[field.Key] = field.Value;
            }
        }

        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        var lines = new List<string>
        {
            FormatField("Timestamp", DateTimeOffset.Now)
            , FormatField("Level", logEntry.LogLevel)
            , FormatField("Message", message)
            , FormatField("Source", fields.GetValueOrDefault("Source") ?? logEntry.Category)
            , FormatField("ThreadId", Environment.CurrentManagedThreadId)
        };
        var logType = fields.GetValueOrDefault("LogType")?.ToString();
        if (string.Equals(logType, "Api", StringComparison.OrdinalIgnoreCase)) {
            AddFields(
                lines, fields, "RequestMethod", "ClientIp", "ServerIp", "UserAgent", "RequestRelativeUrl", "ElapsedMilliseconds", "StatusCode"
                , "ApiResponseCode", "UserId", "UserName", "RequestBody", "RequestHeaders", "RequestContentType", "RequestId", "ResponseHeaders"
                , "ResponseBody", "ResponseContentType"
            );
        }
        else if (string.Equals(logType, "Sql", StringComparison.OrdinalIgnoreCase)) {
            AddFields(lines, fields, "Sql", "ElapsedMilliseconds");
        }
        else if (string.Equals(logType, "Http", StringComparison.OrdinalIgnoreCase)) {
            AddFields(
                lines, fields, "RequestMethod", "RequestUrl", "ElapsedMilliseconds", "StatusCode", "RequestBody", "RequestHeaders"
                , "RequestContentType", "ResponseHeaders", "ResponseBody", "ResponseContentType"
            );
        }

        if (logEntry.Exception is not null) {
            lines.Add(FormatField("Exception", logEntry.Exception));
        }

        var content = string.Join(Environment.NewLine, lines);
        var color = GetColor(logEntry.LogLevel);
        textWriter.Write(color);

        textWriter.Write(content);
        textWriter.Write(Environment.NewLine);
        textWriter.Write(new string('-', 100));
        textWriter.Write(Environment.NewLine);

        textWriter.Write(_RESET_COLOR);
    }

    private static void AddFields(
        List<string> lines
        , IReadOnlyDictionary<string, object?> fields
        , params string[] names
    ) {
        foreach (var name in names) {
            lines.Add(FormatField(name, fields.GetValueOrDefault(name)));
        }
    }

    private static string FormatField(
        string name
        , object? value
    ) {
        return $"[{name}]: {value ?? string.Empty}";
    }

    /// <summary>
    ///     获取日志级别对应的 ANSI 颜色
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>ANSI 颜色转义序列</returns>
    private static string GetColor(LogLevel logLevel) {
        return logLevel switch
        {
            LogLevel.Information => _GREEN_COLOR
            , LogLevel.Warning => _YELLOW_COLOR
            , >= LogLevel.Error => _RED_COLOR
            , _ => _GRAY_COLOR
        };
    }
}