using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Logging;

/// <summary>
///     从动态筛选树中解析创建时间范围，用来决定需要查询哪些日分表；请求日志按天分表，查询必须有明确且不过大的日期范围
/// </summary>
public static class ApiRequestLogQueryRange
{
    /// <summary>
    ///     单次查询最多跨越的天数（含首尾两天）
    /// </summary>
    private const int _MAX_DAYS = 7;

    /// <summary>
    ///     解析筛选树中 And 关系下的创建时间范围，未指定范围时默认只查今天
    /// </summary>
    /// <param name="filter">动态筛选根节点</param>
    /// <param name="now">当前 UTC 时间</param>
    /// <returns>起止日期，均为 UTC 日期，含首尾</returns>
    /// <exception cref="DynamicFilterValidationException">范围格式无效、起止颠倒或跨度超过上限时引发</exception>
    public static (DateTime From, DateTime To) Resolve(
        DynamicFilter? filter
        , DateTime now
    ) {
        var range = FindCreatedAtRange(filter);
        if (range is null) {
            return (now.Date, now.Date);
        }

        var (start, end) = range.Value;

        // 右开区间：结束时刻恰好是零点时，实际只包含前一天
        var lastDay = end.TimeOfDay == TimeSpan.Zero && end > start ? end.AddTicks(-1).Date : end.Date;
        if (lastDay < start.Date) {
            throw new DynamicFilterValidationException("The created time range is invalid.");
        }

        return (lastDay - start.Date).TotalDays + 1 > _MAX_DAYS
            ? throw new DynamicFilterValidationException($"The created time range cannot exceed {_MAX_DAYS} days.")
            : (start.Date, lastDay);
    }

    /// <summary>
    ///     在 And 关系的筛选树里查找 CreatedAt 的 DateRange 或 Range 条件，Or 分支内的条件不能限定整体范围，因此不采用
    /// </summary>
    /// <param name="filter">筛选节点</param>
    /// <returns>起止时间，没有时返回 null</returns>
    private static (DateTime Start, DateTime End)? FindCreatedAtRange(DynamicFilter? filter) {
        var node = filter?.NestedDynamicFilter ?? filter;
        if (node is null) {
            return null;
        }

        if (string.Equals(node.Field?.Trim(), "CreatedAt", StringComparison.OrdinalIgnoreCase)
            && node.Operator is { } op
            && (op.Equals("DateRange", StringComparison.OrdinalIgnoreCase) || op.Equals("Range", StringComparison.OrdinalIgnoreCase))
            && node.Value is { ValueKind: JsonValueKind.Array } value) {
            return ReadRange(value);
        }

        if (!string.Equals(node.Logic, "And", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return node.Filters.Select(FindCreatedAtRange).FirstOrDefault(x => x is not null);
    }

    /// <summary>
    ///     把日期文本解析为 UTC 时间
    /// </summary>
    /// <param name="item">JSON 字符串元素</param>
    /// <returns>UTC 时间</returns>
    /// <exception cref="DynamicFilterValidationException">日期格式无效时引发</exception>
    private static DateTime ParseUtc(JsonElement item) {
        return item.ValueKind == JsonValueKind.String
               && DateTimeOffset.TryParse(item.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : throw new DynamicFilterValidationException("The created time range contains an invalid date.");
    }

    /// <summary>
    ///     读取两个元素的日期数组，日期文本按 UTC 解析，仅有日期时视为当天零点
    /// </summary>
    /// <param name="value">JSON 数组</param>
    /// <returns>起止时间</returns>
    /// <exception cref="DynamicFilterValidationException">数组元素数量不是 2 或日期格式无效时引发</exception>
    private static (DateTime Start, DateTime End) ReadRange(JsonElement value) {
        var items = value.EnumerateArray().ToArray();
        return items.Length != 2
            ? throw new DynamicFilterValidationException("The created time range requires exactly two values.")
            : (ParseUtc(items[0]), ParseUtc(items[1]));
    }
}