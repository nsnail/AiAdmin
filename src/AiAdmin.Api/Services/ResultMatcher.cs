using System.Globalization;
using System.Text.RegularExpressions;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     值与阈值的通用比较：支持数字比较、字符串比较和正则匹配，告警监控和计划作业结果报警共用，纯函数，不访问数据库和 Redis
/// </summary>
public static class ResultMatcher
{
    /// <summary>
    ///     数字比较允许的操作符
    /// </summary>
    private static readonly AlertMonitorOperator[] _numberOperators =
    [
        AlertMonitorOperator.GreaterThan, AlertMonitorOperator.GreaterThanOrEqual, AlertMonitorOperator.LessThan
        , AlertMonitorOperator.LessThanOrEqual, AlertMonitorOperator.Equal, AlertMonitorOperator.NotEqual
    ];

    /// <summary>
    ///     正则匹配的超时时间，防止写坏的正则回溯过久卡住调用线程
    /// </summary>
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     字符串比较允许的操作符
    /// </summary>
    private static readonly AlertMonitorOperator[] _stringOperators =
    [
        AlertMonitorOperator.Equal, AlertMonitorOperator.NotEqual, AlertMonitorOperator.Contains, AlertMonitorOperator.NotContains
        , AlertMonitorOperator.StartsWith, AlertMonitorOperator.EndsWith, AlertMonitorOperator.RegexMatch, AlertMonitorOperator.RegexNotMatch
    ];

    /// <summary>
    ///     判断操作符是否适用于比较类型
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="op">操作符</param>
    /// <returns>适用返回 true</returns>
    public static bool IsOperatorAllowed(
        AlertMonitorValueType valueType
        , AlertMonitorOperator op
    ) {
        return (valueType == AlertMonitorValueType.Number ? _numberOperators : _stringOperators).Contains(op);
    }

    /// <summary>
    ///     校验阈值：数字比较时必须能解析为数字（使用不变区域性，不接受千分位），正则操作符时必须是可编译的正则，其余字符串比较时不能为空
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="op">操作符</param>
    /// <param name="threshold">阈值文本</param>
    /// <returns>阈值合法返回 true</returns>
    public static bool IsThresholdValid(
        AlertMonitorValueType valueType
        , AlertMonitorOperator op
        , string threshold
    ) {
        if (valueType == AlertMonitorValueType.Number) {
            return TryParseNumber(threshold, out _);
        }

        return op is AlertMonitorOperator.RegexMatch or AlertMonitorOperator.RegexNotMatch
            ? IsRegexValid(threshold)
            : !string.IsNullOrEmpty(threshold);
    }

    /// <summary>
    ///     按比较类型比较值与阈值
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值文本，正则操作符时为正则表达式</param>
    /// <param name="caseSensitive">字符串和正则比较是否区分大小写</param>
    /// <returns>比较结果，值或阈值无法按比较类型处理时返回带原因的失败结果</returns>
    public static MatchResult Match(
        AlertMonitorValueType valueType
        , AlertMonitorOperator op
        , string value
        , string threshold
        , bool caseSensitive
    ) {
        return valueType == AlertMonitorValueType.Number ? MatchNumber(op, value, threshold) : MatchText(op, value, threshold, caseSensitive);
    }

    /// <summary>
    ///     解析数字：使用不变区域性，只接受普通十进制和科学计数法，不接受千分位和货币符号
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="number">解析出的数字</param>
    /// <returns>解析成功返回 true</returns>
    public static bool TryParseNumber(
        string text
        , out decimal number
    ) {
        return decimal.TryParse(
            text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture
            , out number
        );
    }

    /// <summary>
    ///     判断文本是否是可编译的正则表达式
    /// </summary>
    /// <param name="pattern">正则表达式</param>
    /// <returns>可编译返回 true，空表达式视为不合法</returns>
    private static bool IsRegexValid(string pattern) {
        if (string.IsNullOrEmpty(pattern)) {
            return false;
        }

        try {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, _regexTimeout);
            return true;
        }
        catch (ArgumentException) {
            return false;
        }
    }

    /// <summary>
    ///     数字比较，值或阈值不是数字时返回带原因的失败结果
    /// </summary>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值文本</param>
    /// <returns>比较结果</returns>
    private static MatchResult MatchNumber(
        AlertMonitorOperator op
        , string value
        , string threshold
    ) {
        if (!TryParseNumber(value, out var actual)) {
            return MatchResult.Invalid("Value is not a number");
        }

        if (!TryParseNumber(threshold, out var limit)) {
            return MatchResult.Invalid("Threshold is not a number");
        }

        return MatchResult.Ok(
            op switch
            {
                AlertMonitorOperator.GreaterThan => actual > limit
                , AlertMonitorOperator.GreaterThanOrEqual => actual >= limit
                , AlertMonitorOperator.LessThan => actual < limit
                , AlertMonitorOperator.LessThanOrEqual => actual <= limit
                , AlertMonitorOperator.Equal => actual == limit
                , AlertMonitorOperator.NotEqual => actual != limit
                , _ => false
            }
        );
    }

    /// <summary>
    ///     正则匹配，阈值不是合法正则或匹配超时时返回带原因的失败结果
    /// </summary>
    /// <param name="op">正则匹配或不匹配</param>
    /// <param name="value">读取到的值</param>
    /// <param name="pattern">正则表达式</param>
    /// <param name="caseSensitive">是否区分大小写</param>
    /// <returns>比较结果</returns>
    private static MatchResult MatchRegex(
        AlertMonitorOperator op
        , string value
        , string pattern
        , bool caseSensitive
    ) {
        try {
            var options = caseSensitive ? RegexOptions.CultureInvariant : RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
            var matched = Regex.IsMatch(value, pattern, options, _regexTimeout);
            return MatchResult.Ok(op == AlertMonitorOperator.RegexMatch ? matched : !matched);
        }
        catch (RegexMatchTimeoutException) {
            return MatchResult.Invalid("Regular expression timed out");
        }
        catch (ArgumentException) {
            return MatchResult.Invalid("Threshold is not a valid regular expression");
        }
    }

    /// <summary>
    ///     字符串比较，正则操作符转交正则匹配
    /// </summary>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值文本</param>
    /// <param name="caseSensitive">是否区分大小写</param>
    /// <returns>比较结果</returns>
    private static MatchResult MatchText(
        AlertMonitorOperator op
        , string value
        , string threshold
        , bool caseSensitive
    ) {
        if (op is AlertMonitorOperator.RegexMatch or AlertMonitorOperator.RegexNotMatch) {
            return MatchRegex(op, value, threshold, caseSensitive);
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return MatchResult.Ok(
            op switch
            {
                AlertMonitorOperator.Equal => string.Equals(value, threshold, comparison)
                , AlertMonitorOperator.NotEqual => !string.Equals(value, threshold, comparison)
                , AlertMonitorOperator.Contains => value.Contains(threshold, comparison)
                , AlertMonitorOperator.NotContains => !value.Contains(threshold, comparison)
                , AlertMonitorOperator.StartsWith => value.StartsWith(threshold, comparison)
                , AlertMonitorOperator.EndsWith => value.EndsWith(threshold, comparison)
                , _ => false
            }
        );
    }
}