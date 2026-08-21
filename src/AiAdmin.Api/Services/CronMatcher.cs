using System.Globalization;

namespace AiAdmin.Api.Services;

/// <summary>
///     匹配计划作业 Cron 表达式
/// </summary>
internal static class CronMatcher
{
    /// <summary>
    ///     判断 Cron 表达式在指定时间是否到期
    /// </summary>
    /// <param name="expression">Cron 表达式</param>
    /// <param name="now">待匹配时间</param>
    /// <returns>到期时返回 true</returns>
    public static bool IsDue(
        string expression
        , DateTime now
    ) {
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length switch
        {
            5 => Match(fields[0], now.Minute, 0, 59)
                 && Match(fields[1], now.Hour, 0, 23)
                 && Match(fields[2], now.Day, 1, 31)
                 && Match(fields[3], now.Month, 1, 12)
                 && Match(fields[4], (int)now.DayOfWeek, 0, 6)
            , 6 => Match(fields[0], now.Second, 0, 59)
                   && Match(fields[1], now.Minute, 0, 59)
                   && Match(fields[2], now.Hour, 0, 23)
                   && Match(fields[3], now.Day, 1, 31)
                   && Match(fields[4], now.Month, 1, 12)
                   && Match(fields[5], (int)now.DayOfWeek, 0, 6)
            , _ => false
        };
    }

    /// <summary>
    ///     判断作业是否仍处于同一个触发时间窗口
    /// </summary>
    /// <param name="expression">Cron 表达式</param>
    /// <param name="previous">上一次触发时间</param>
    /// <param name="current">当前时间</param>
    /// <returns>是否属于同一个触发窗口</returns>
    public static bool IsSameTriggerWindow(
        string expression
        , DateTime previous
        , DateTime current
    ) {
        var format = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6 ? "yyyyMMddHHmmss" : "yyyyMMddHHmm";
        return previous.ToString(format, CultureInfo.InvariantCulture) == current.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     判断 Cron 字段是否匹配指定数值
    /// </summary>
    /// <param name="field">Cron 字段</param>
    /// <param name="value">待匹配数值</param>
    /// <param name="min">字段最小值</param>
    /// <param name="max">字段最大值</param>
    /// <returns>匹配时返回 true</returns>
    private static bool Match(
        string field
        , int value
        , int min
        , int max
    ) {
        return field
            .Split(',')
            .Any(part =>
                {
                    var pieces = part.Split('/');
                    var step = pieces.Length == 2 && int.TryParse(pieces[1], out var parsedStep) ? parsedStep : 1;
                    (int, int) range;
                    if (step <= 0) {
                        return false;
                    }

                    if (pieces[0] == "*" || (pieces.Length == 2 && int.TryParse(pieces[0], out _))) {
                        // 数字步长（如 0/5）从指定起点延伸到字段上限
                        var start = pieces[0] == "*" ? min : int.Parse(pieces[0], CultureInfo.InvariantCulture);
                        range = (start, max);
                    }
                    else if (pieces[0].Contains('-')
                             && pieces[0].Split('-') is [var a, var b]
                             && int.TryParse(a, out var start)
                             && int.TryParse(b, out var end)) {
                        range = (start, end);
                    }
                    else if (int.TryParse(pieces[0], out var exact)) {
                        range = (exact, exact);
                    }
                    else {
                        range = (int.MinValue, int.MinValue);
                    }

                    return value >= range.Item1 && value <= range.Item2 && (value - range.Item1) % step == 0;
                }
            );
    }
}