using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证共用的值比较器：文本正则匹配与不匹配、大小写、非法正则、匹配超时，以及数字比较在值或阈值无法解析时的失败结果
/// </summary>
public sealed class ResultMatcherTests
{
    /// <summary>
    ///     灾难性回溯的正则会在超时后返回失败结果，而不是卡住调用线程
    /// </summary>
    [Fact]
    public void CatastrophicRegexTimesOutInsteadOfHanging() {
        var value = new string('a', 40) + "!";

        var result = ResultMatcher.Match(AlertMonitorValueType.Text, AlertMonitorOperator.RegexMatch, value, "^(a+)+$", false);

        Assert.True(result.Failed);
        Assert.Equal("Regular expression timed out", result.Reason);
    }

    /// <summary>
    ///     阈值不是合法正则时返回失败结果并带原因，调用方按取不到有效值处理，不会抛出异常
    /// </summary>
    [Fact]
    public void InvalidRegexFailsWithReason() {
        var result = ResultMatcher.Match(AlertMonitorValueType.Text, AlertMonitorOperator.RegexMatch, "anything", "(unclosed", false);

        Assert.True(result.Failed);
        Assert.True(result.Hit);
        Assert.Equal("Threshold is not a valid regular expression", result.Reason);
    }

    /// <summary>
    ///     告警监控使用正则操作符时：命中即报警，非法正则按取不到有效值报警并说明原因，正常不命中则不报警
    /// </summary>
    [Fact]
    public void MonitorWithRegexRaisesOnMatchAndOnInvalidPattern() {
        var monitor = new AlertMonitor
        {
            Name = "m"
            , Target = "t"
            , Source = AlertMonitorSource.Dictionary
            , CategoryCode = "c"
            , ValueType = AlertMonitorValueType.Text
            , Operator = AlertMonitorOperator.RegexMatch
            , Threshold = "fail(ed|ure)"
        };

        Assert.True(AlertMonitorEvaluator.Evaluate(monitor, "job Failure at 3", string.Empty).Breached);
        Assert.False(AlertMonitorEvaluator.Evaluate(monitor, "job succeeded", string.Empty).Breached);

        monitor.Threshold = "(broken";
        var invalid = AlertMonitorEvaluator.Evaluate(monitor, "job succeeded", string.Empty);
        Assert.True(invalid.Breached);
        Assert.Equal("Threshold is not a valid regular expression", invalid.Message);
    }

    /// <summary>
    ///     数字比较时值不是数字或阈值不是数字，返回对应原因的失败结果
    /// </summary>
    [Fact]
    public void NumberComparisonFailsWhenValueOrThresholdIsNotNumeric() {
        var badValue = ResultMatcher.Match(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "OK", "10", false);
        var badThreshold = ResultMatcher.Match(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, "11", "ten", false);
        var good = ResultMatcher.Match(AlertMonitorValueType.Number, AlertMonitorOperator.GreaterThan, " 11 ", "10", false);

        Assert.Equal((true, "Value is not a number"), (badValue.Failed, badValue.Reason));
        Assert.Equal((true, "Threshold is not a number"), (badThreshold.Failed, badThreshold.Reason));
        Assert.Equal((false, true), (good.Failed, good.Hit));
    }

    /// <summary>
    ///     区分大小写时大小写不同的值不匹配，不区分时匹配
    /// </summary>
    [Fact]
    public void RegexHonorsCaseSensitivity() {
        Assert.True(ResultMatcher.Match(AlertMonitorValueType.Text, AlertMonitorOperator.RegexMatch, "ERROR", "error", false).Hit);
        Assert.False(ResultMatcher.Match(AlertMonitorValueType.Text, AlertMonitorOperator.RegexMatch, "ERROR", "error", true).Hit);
        Assert.True(ResultMatcher.Match(AlertMonitorValueType.Text, AlertMonitorOperator.RegexNotMatch, "ERROR", "error", true).Hit);
    }

    /// <summary>
    ///     正则匹配与不匹配：默认不区分大小写，匹配的是子串而不是整串，需要整串匹配时由表达式自己加锚点
    /// </summary>
    /// <param name="op">正则匹配或不匹配</param>
    /// <param name="value">读取到的值</param>
    /// <param name="pattern">正则表达式</param>
    /// <param name="expected">期望是否命中</param>
    [Theory]
    [InlineData(AlertMonitorOperator.RegexMatch, "ERR-42 happened", "err-\\d+", true)]
    [InlineData(AlertMonitorOperator.RegexMatch, "all good", "err-\\d+", false)]
    [InlineData(AlertMonitorOperator.RegexMatch, "{\"code\":200,\"msg\":\"OK\"}", "\"code\"\\s*:\\s*200", true)]
    [InlineData(AlertMonitorOperator.RegexMatch, "x ok y", "^ok$", false)]
    [InlineData(AlertMonitorOperator.RegexMatch, "ok", "^ok$", true)]
    [InlineData(AlertMonitorOperator.RegexNotMatch, "{\"code\":200}", "\"code\"\\s*:\\s*200", false)]
    [InlineData(AlertMonitorOperator.RegexNotMatch, "{\"code\":500}", "\"code\"\\s*:\\s*200", true)]
    [InlineData(AlertMonitorOperator.RegexNotMatch, "", "ok", true)]
    public void RegexMatchesSubstringAndIgnoresCaseByDefault(
        AlertMonitorOperator op
        , string value
        , string pattern
        , bool expected
    ) {
        var result = ResultMatcher.Match(AlertMonitorValueType.Text, op, value, pattern, false);

        Assert.False(result.Failed);
        Assert.Equal(expected, result.Hit);
    }
}