using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     告警监控的判定逻辑：把读取到的值与阈值按操作符比较，纯函数，不访问数据库和 Redis
/// </summary>
public static class AlertMonitorEvaluator
{
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

        var result = ResultMatcher.Match(monitor.ValueType, monitor.Operator, value, monitor.Threshold, monitor.CaseSensitive);
        return result.Failed
            ? new AlertMonitorOutcome(true, value, result.Reason)
            : new AlertMonitorOutcome(result.Hit, value, result.Hit ? "Condition matched" : "Normal");
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
        return ResultMatcher.IsOperatorAllowed(valueType, op);
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
        return ResultMatcher.IsThresholdValid(valueType, op, threshold);
    }
}