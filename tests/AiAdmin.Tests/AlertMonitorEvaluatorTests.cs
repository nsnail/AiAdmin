using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证告警监控的判定与状态机：数字比较、字符串比较、取不到值视为触发、长度指标的组合限制，以及触发和解除只发一次
/// </summary>
public sealed class AlertMonitorEvaluatorTests
{
    private static readonly DateTime _now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    ///     开启区分大小写后，大小写不同不再相等
    /// </summary>
    [Fact]
    public void CaseSensitiveComparisonDistinguishesCase() {
        var monitor = NewMonitor(AlertMonitorValueType.Text, AlertMonitorOperator.Equal, "ok");
        monitor.CaseSensitive = true;

        Assert.False(AlertMonitorEvaluator.Evaluate(monitor, "OK", string.Empty).Breached);
        Assert.True(AlertMonitorEvaluator.Evaluate(monitor, "ok", string.Empty).Breached);
    }

    /// <summary>
    ///     长度监控的通知文本标明是长度
    /// </summary>
    [Fact]
    public void LengthAlertTextSaysLength() {
        var monitor = NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "1000");
        monitor.Source = AlertMonitorSource.Redis;
        monitor.Metric = AlertMonitorMetric.Length;
        monitor.Target = "queue:report";

        var text = AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "2500", string.Empty), _now);

        Assert.NotNull(text);
        Assert.Contains("Target: Redis queue:report", text, StringComparison.Ordinal);
        Assert.Contains("Condition: Length GreaterThan 1000", text, StringComparison.Ordinal);
        Assert.Contains("Length: 2500", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     长度以数字文本传入判定器：队列积压超过阈值触发，长度为 0 时按小于判定命中
    /// </summary>
    [Fact]
    public void LengthIsComparedAsNumber() {
        var backlog = NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "1000");
        backlog.Metric = AlertMonitorMetric.Length;

        Assert.True(AlertMonitorEvaluator.Evaluate(backlog, "1001", string.Empty).Breached);
        Assert.False(AlertMonitorEvaluator.Evaluate(backlog, "1000", string.Empty).Breached);

        var drained = NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.LessThanOrEqual, "0");
        drained.Metric = AlertMonitorMetric.Length;

        Assert.True(AlertMonitorEvaluator.Evaluate(drained, "0", string.Empty).Breached);
    }

    /// <summary>
    ///     长度指标只能用于 Redis 来源且必须按数字比较，值指标不受限制
    /// </summary>
    /// <param name="source">数据来源</param>
    /// <param name="metric">指标</param>
    /// <param name="valueType">比较类型</param>
    /// <param name="expected">期望是否允许</param>
    [Theory]
    [InlineData(AlertMonitorSource.Redis, AlertMonitorMetric.Length, AlertMonitorValueType.Number, true)]
    [InlineData(AlertMonitorSource.Redis, AlertMonitorMetric.Length, AlertMonitorValueType.Text, false)]
    [InlineData(AlertMonitorSource.Dictionary, AlertMonitorMetric.Length, AlertMonitorValueType.Number, false)]
    [InlineData(AlertMonitorSource.Dictionary, AlertMonitorMetric.Value, AlertMonitorValueType.Text, true)]
    [InlineData(AlertMonitorSource.Redis, AlertMonitorMetric.Value, AlertMonitorValueType.Text, true)]
    public void LengthMetricIsRedisAndNumberOnly(
        AlertMonitorSource source
        , AlertMonitorMetric metric
        , AlertMonitorValueType valueType
        , bool expected
    ) {
        Assert.Equal(expected, AlertMonitorEvaluator.IsMetricAllowed(source, metric, valueType));
    }

    /// <summary>
    ///     取不到值（键不存在、字典项被删或停用）视为触发，并带上原因
    /// </summary>
    [Fact]
    public void MissingValueBreachesWithReason() {
        var outcome = AlertMonitorEvaluator.Evaluate(
            NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "1"), null, "Redis key does not exist"
        );

        Assert.True(outcome.Breached);
        Assert.Equal("Redis key does not exist", outcome.Message);
        Assert.Equal(string.Empty, outcome.Value);
    }

    /// <summary>
    ///     取不到值触发报警，值重新出现且正常后解除；通知里写明原因
    /// </summary>
    [Fact]
    public void MissingValueRaisesThenRecoveryResolves() {
        var monitor = NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "10");

        var raised = AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, null, "Redis key does not exist"), _now);
        Assert.NotNull(raised);
        Assert.Contains("Reason: Redis key does not exist", raised, StringComparison.Ordinal);
        Assert.Contains("Value: -", raised, StringComparison.Ordinal);

        var resolved = AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "1", string.Empty), _now.AddMinutes(1));
        Assert.NotNull(resolved);
        Assert.False(monitor.AlertActive);
    }

    /// <summary>
    ///     数字比较时值不是数字或阈值不是数字，都视为取不到有效值，触发并说明原因
    /// </summary>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值</param>
    /// <param name="reason">期望的原因</param>
    [Theory]
    [InlineData("abc", "10", "Value is not a number")]
    [InlineData("1,000", "10", "Value is not a number")]
    [InlineData("", "10", "Value is not a number")]
    [InlineData("10", "ten", "Threshold is not a number")]
    public void NumberComparisonWithNonNumbersBreaches(
        string value
        , string threshold
        , string reason
    ) {
        var outcome = AlertMonitorEvaluator.Evaluate(
            NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, threshold), value, string.Empty
        );

        Assert.True(outcome.Breached);
        Assert.Equal(reason, outcome.Message);
    }

    /// <summary>
    ///     数字比较的六个操作符，边界值取等号两侧
    /// </summary>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值</param>
    /// <param name="expected">期望是否命中</param>
    [Theory]
    [InlineData(AlertMonitorOperator.GreaterThan, "10", "9", true)]
    [InlineData(AlertMonitorOperator.GreaterThan, "10", "10", false)]
    [InlineData(AlertMonitorOperator.GreaterThanOrEqual, "10", "10", true)]
    [InlineData(AlertMonitorOperator.GreaterThanOrEqual, "9.99", "10", false)]
    [InlineData(AlertMonitorOperator.LessThan, "-1", "0", true)]
    [InlineData(AlertMonitorOperator.LessThan, "0", "0", false)]
    [InlineData(AlertMonitorOperator.LessThanOrEqual, "0", "0", true)]
    [InlineData(AlertMonitorOperator.LessThanOrEqual, "0.1", "0", false)]
    [InlineData(AlertMonitorOperator.Equal, "10.0", "10", true)]
    [InlineData(AlertMonitorOperator.Equal, "1e1", "10", true)]
    [InlineData(AlertMonitorOperator.NotEqual, "10", "10", false)]
    [InlineData(AlertMonitorOperator.NotEqual, " 11 ", "10", true)]
    public void NumberOperatorsCompareNumerically(
        AlertMonitorOperator op
        , string value
        , string threshold
        , bool expected
    ) {
        var outcome = AlertMonitorEvaluator.Evaluate(NewMonitor(AlertMonitorValueType.Number, op, threshold), value, string.Empty);

        Assert.Equal(expected, outcome.Breached);
    }

    /// <summary>
    ///     操作符与比较类型的组合：数字不接受文本操作符，文本不接受大小比较
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="op">操作符</param>
    /// <param name="expected">期望是否允许</param>
    [Theory]
    [InlineData(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, true)]
    [InlineData(AlertMonitorValueType.Number, AlertMonitorOperator.NotEqual, true)]
    [InlineData(AlertMonitorValueType.Number, AlertMonitorOperator.Contains, false)]
    [InlineData(AlertMonitorValueType.Number, AlertMonitorOperator.EndsWith, false)]
    [InlineData(AlertMonitorValueType.Text, AlertMonitorOperator.Contains, true)]
    [InlineData(AlertMonitorValueType.Text, AlertMonitorOperator.Equal, true)]
    [InlineData(AlertMonitorValueType.Text, AlertMonitorOperator.GreaterThan, false)]
    [InlineData(AlertMonitorValueType.Text, AlertMonitorOperator.LessThanOrEqual, false)]
    public void OperatorMustMatchValueType(
        AlertMonitorValueType valueType
        , AlertMonitorOperator op
        , bool expected
    ) {
        Assert.Equal(expected, AlertMonitorEvaluator.IsOperatorAllowed(valueType, op));
    }

    /// <summary>
    ///     状态机：触发只发一次，持续异常不重复，恢复发一次解除，之后正常不再发
    /// </summary>
    [Fact]
    public void RaisedAndResolvedAreSentOnceEach() {
        var monitor = NewMonitor(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "10");
        monitor.Mentions = "@alice";

        var raised = AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "11", string.Empty), _now);
        Assert.NotNull(raised);
        Assert.StartsWith("\U0001F525 ALERT:", raised, StringComparison.Ordinal);
        Assert.EndsWith("@alice", raised, StringComparison.Ordinal);
        Assert.True(monitor.AlertActive);
        Assert.Equal(_now, monitor.AlertedAt);

        Assert.Null(AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "50", string.Empty), _now.AddMinutes(1)));
        Assert.True(monitor.AlertActive);
        Assert.Equal("50", monitor.LastValue);

        var resolved = AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "3", string.Empty), _now.AddMinutes(2));
        Assert.NotNull(resolved);
        Assert.StartsWith("✅ RESOLVED:", resolved, StringComparison.Ordinal);
        Assert.DoesNotContain("@alice", resolved, StringComparison.Ordinal);
        Assert.False(monitor.AlertActive);

        Assert.Null(AlertMonitorScanService.ApplyOutcome(monitor, AlertMonitorEvaluator.Evaluate(monitor, "3", string.Empty), _now.AddMinutes(3)));
        Assert.Equal(_now.AddMinutes(3), monitor.LastCheckedAt);
    }

    /// <summary>
    ///     字符串比较的六个操作符
    /// </summary>
    /// <param name="op">操作符</param>
    /// <param name="value">读取到的值</param>
    /// <param name="threshold">阈值</param>
    /// <param name="expected">期望是否命中</param>
    [Theory]
    [InlineData(AlertMonitorOperator.Equal, "OK", "ok", true)]
    [InlineData(AlertMonitorOperator.NotEqual, "OK", "ok", false)]
    [InlineData(AlertMonitorOperator.NotEqual, "FAIL", "ok", true)]
    [InlineData(AlertMonitorOperator.Contains, "disk is FULL now", "full", true)]
    [InlineData(AlertMonitorOperator.NotContains, "all good", "error", true)]
    [InlineData(AlertMonitorOperator.NotContains, "an Error here", "error", false)]
    [InlineData(AlertMonitorOperator.StartsWith, "ERR-42", "err", true)]
    [InlineData(AlertMonitorOperator.StartsWith, "x-ERR", "err", false)]
    [InlineData(AlertMonitorOperator.EndsWith, "job failed", "FAILED", true)]
    [InlineData(AlertMonitorOperator.EndsWith, "failed job", "failed", false)]
    public void StringOperatorsIgnoreCaseByDefault(
        AlertMonitorOperator op
        , string value
        , string threshold
        , bool expected
    ) {
        var outcome = AlertMonitorEvaluator.Evaluate(NewMonitor(AlertMonitorValueType.Text, op, threshold), value, string.Empty);

        Assert.Equal(expected, outcome.Breached);
    }

    /// <summary>
    ///     阈值校验：数字比较必须是数字，文本比较不能为空
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="threshold">阈值</param>
    /// <param name="expected">期望是否合法</param>
    [Theory]
    [InlineData(AlertMonitorValueType.Number, "100", true)]
    [InlineData(AlertMonitorValueType.Number, "-0.5", true)]
    [InlineData(AlertMonitorValueType.Number, "abc", false)]
    [InlineData(AlertMonitorValueType.Number, "", false)]
    [InlineData(AlertMonitorValueType.Number, "1,000", false)]
    [InlineData(AlertMonitorValueType.Text, "error", true)]
    [InlineData(AlertMonitorValueType.Text, "", false)]
    public void ThresholdMustMatchValueType(
        AlertMonitorValueType valueType
        , string threshold
        , bool expected
    ) {
        Assert.Equal(expected, AlertMonitorEvaluator.IsThresholdValid(valueType, threshold));
    }

    /// <summary>
    ///     创建告警监控
    /// </summary>
    /// <param name="valueType">比较类型</param>
    /// <param name="op">操作符</param>
    /// <param name="threshold">阈值</param>
    /// <returns>告警监控</returns>
    private static AlertMonitor NewMonitor(
        AlertMonitorValueType valueType
        , AlertMonitorOperator op
        , string threshold
    ) {
        return new AlertMonitor
        {
            Name = "m"
            , Target = "t"
            , Source = AlertMonitorSource.Dictionary
            , CategoryCode = "c"
            , ValueType = valueType
            , Operator = op
            , Threshold = threshold
        };
    }
}