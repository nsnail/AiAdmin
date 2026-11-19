using System.Globalization;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     告警监控的判定逻辑：把读取到的值与阈值按操作符比较，纯函数，不访问数据库和 Redis
/// </summary>
public static class AlertMonitorEvaluator
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
    ///     字符串比较允许的操作符
    /// </summary>
    private static readonly AlertMonitorOperator[] _stringOperators =
    [
        AlertMonitorOperator.Equal, AlertMonitorOperator.NotEqual, AlertMonitorOperator.Contains, AlertMonitorOperator.NotContains
        , AlertMonitorOperator.StartsWith, AlertMonitorOperator.EndsWith
    ];

    /// <summary>
    ///     对读取到的值执行判定
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="value">读取到的值，取不到值时为 null</param>
    /// <param name="missingReason">取不到值的原因，仅在 value 为 null 时使用</param>
    /// <returns>判定结果</returns>
    public static AlertMonitorOutcome Evaluate(
        AlertMonitor monitor
        , string? value
        , string missingReason
    ) {
        if (value is null) {
            return new AlertMonitorOutcome(true, string.Empty, missingReason);
        }

        if (monitor.ValueType == AlertMonitorValueType.Number) {
            return EvaluateNumber(monitor, value);
        }

        var hit = CompareString(monitor.Operator, value, monitor.Threshold, monitor.CaseSensitive);
        return new AlertMonitorOutcome(hit, value, hit ? "Condition matched" : "Normal");
    }

    /// <summary>
    ///     校验指标与来源、比较类型的组合：长度只能用于 Redis 来源，且必须按数字比较
    /// </summary>
    /// <param name="source">数据来源</param>
    /// <param name="metric">比较的指标</param>
    /// <param name="valueType">比较类型</param>
    /// <returns>组合合法返回 true</returns>
    public static bool IsMetricAllowed(
        AlertMonitorSource source
        , AlertMonitorMetric metric
        , AlertMonitorValueType valueType
    ) {
        return metric == AlertMonitorMetric.Value || (source == AlertMonitorSource.Redis && valueType == AlertMonitorValueType.Number);
    }

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
    ///     校验阈值：数字比较时必须能解析为数字（使用不变区域性，不接受千分位），字符串比较时不能为空
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="threshold">阈值文本</param>
    /// <returns>阈值合法返回 true</returns>
    public static bool IsThresholdValid(
        AlertMonitorValueType valueType
        , string threshold
    ) {
        return valueType == AlertMonitorValueType.Number ? TryParseNumber(threshold, out _) : !string.IsNullOrEmpty(threshold);
    }

    /// <summary>
    ///     字符串比较
    /// </summary>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值文本</param>
    /// <param name="caseSensitive">是否区分大小写</param>
    /// <returns>命中返回 true，不适用于字符串的操作符返回 false</returns>
    private static bool CompareString(
        AlertMonitorOperator op
        , string value
        , string threshold
        , bool caseSensitive
    ) {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return op switch
        {
            AlertMonitorOperator.Equal => string.Equals(value, threshold, comparison)
            , AlertMonitorOperator.NotEqual => !string.Equals(value, threshold, comparison)
            , AlertMonitorOperator.Contains => value.Contains(threshold, comparison)
            , AlertMonitorOperator.NotContains => !value.Contains(threshold, comparison)
            , AlertMonitorOperator.StartsWith => value.StartsWith(threshold, comparison)
            , AlertMonitorOperator.EndsWith => value.EndsWith(threshold, comparison)
            , _ => false
        };
    }

    /// <summary>
    ///     数字比较，值或阈值不是数字时视为取不到有效值，触发报警并说明原因
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="value">读取到的值</param>
    /// <returns>判定结果</returns>
    private static AlertMonitorOutcome EvaluateNumber(
        AlertMonitor monitor
        , string value
    ) {
        if (!TryParseNumber(value, out var actual)) {
            return new AlertMonitorOutcome(true, value, "Value is not a number");
        }

        if (!TryParseNumber(monitor.Threshold, out var limit)) {
            return new AlertMonitorOutcome(true, value, "Threshold is not a number");
        }

        var hit = monitor.Operator switch
        {
            AlertMonitorOperator.GreaterThan => actual > limit
            , AlertMonitorOperator.GreaterThanOrEqual => actual >= limit
            , AlertMonitorOperator.LessThan => actual < limit
            , AlertMonitorOperator.LessThanOrEqual => actual <= limit
            , AlertMonitorOperator.Equal => actual == limit
            , AlertMonitorOperator.NotEqual => actual != limit
            , _ => false
        };
        return new AlertMonitorOutcome(hit, value, hit ? "Condition matched" : "Normal");
    }

    /// <summary>
    ///     解析数字：使用不变区域性，只接受普通十进制和科学计数法，不接受千分位和货币符号
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="number">解析出的数字</param>
    /// <returns>解析成功返回 true</returns>
    private static bool TryParseNumber(
        string text
        , out decimal number
    ) {
        return decimal.TryParse(
            text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture
            , out number
        );
    }
}