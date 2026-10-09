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
        return field.Split(',').Any(IsMatch);

        bool IsMatch(string part) {
            return MatchPart(part, value, min, max);
        }
    }

    /// <summary>
    ///     判断 Cron 单个逗号片段是否匹配
    /// </summary>
    /// <param name="part">Cron 片段</param>
    /// <param name="value">待匹配数值</param>
    /// <param name="min">字段最小值</param>
    /// <param name="max">字段最大值</param>
    /// <returns>匹配时返回 true</returns>
    private static bool MatchPart(
        string part
        , int value
        , int min
        , int max
    ) {
        var pieces = part.Split('/');
        var step = ParseStep(pieces);
        return step > 0
               && TryGetRange(pieces, min, max, out var range)
               && value >= range.Start
               && value <= range.End
               && (value - range.Start) % step == 0;
    }

    /// <summary>
    ///     解析 Cron 步长
    /// </summary>
    /// <param name="pieces">按步长分隔的 Cron 片段</param>
    /// <returns>步长数值</returns>
    private static int ParseStep(string[] pieces) {
        return pieces.Length == 2 && int.TryParse(pieces[1], out var step) ? step : 1;
    }

    /// <summary>
    ///     解析 Cron 片段范围
    /// </summary>
    /// <param name="pieces">按步长分隔的 Cron 片段</param>
    /// <param name="min">字段最小值</param>
    /// <param name="max">字段最大值</param>
    /// <param name="range">解析出的范围</param>
    /// <returns>解析成功时返回 true</returns>
    private static bool TryGetRange(
        string[] pieces
        , int min
        , int max
        , out (int Start, int End) range
    ) {
        range = default;
        if (pieces.Length == 0) {
            return false;
        }

        var expression = pieces[0];
        if (expression == "*") {
            range = (min, max);
            return true;
        }

        if (pieces.Length == 2 && int.TryParse(expression, CultureInfo.InvariantCulture, out var steppedStart)) {
            range = (steppedStart, max);
            return true;
        }

        var bounds = expression.Split('-');
        if (bounds is [var startText, var endText]
            && int.TryParse(startText, CultureInfo.InvariantCulture, out var start)
            && int.TryParse(endText, CultureInfo.InvariantCulture, out var end)) {
            range = (start, end);
            return true;
        }

        if (!int.TryParse(expression, CultureInfo.InvariantCulture, out var exact)) {
            return false;
        }

        range = (exact, exact);
        return true;
    }
}