using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证计划作业的执行结果报警：按完整响应体匹配文本、数字和正则规则，没有响应体时不判定，规则无法处理时按命中报警，且与失败规则合并为同一个报警状态
/// </summary>
public sealed class ScheduledJobResultAlertTests
{
    /// <summary>
    ///     区分大小写时，大小写不同的响应体不命中
    /// </summary>
    [Fact]
    public void CaseSensitiveRuleDistinguishesCase() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.Contains;
        job.AlertResultValue = "error";
        job.AlertResultCaseSensitive = true;

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "Server ERROR"));
        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "an error"));
    }

    /// <summary>
    ///     正则写坏时按命中报警并说明原因，不会抛出异常，也不会让作业完成流程失败
    /// </summary>
    [Fact]
    public void InvalidRegexAlertsWithReasonInsteadOfThrowing() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.RegexMatch;
        job.AlertResultValue = "(unclosed";

        var text = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 100, string.Empty, true, "anything");

        Assert.NotNull(text);
        Assert.Contains("Result: Threshold is not a valid regular expression", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     没有响应体（超时、异常或仍在运行）时不按结果规则判定：关闭失败报警后，“不包含 OK”这类规则也不会因为没有响应而误报
    /// </summary>
    /// <param name="status">本次状态</param>
    [Theory]
    [InlineData(ScheduledJobStatus.Timeout)]
    [InlineData(ScheduledJobStatus.Failed)]
    [InlineData(ScheduledJobStatus.Running)]
    public void MissingBodySkipsResultRule(ScheduledJobStatus status) {
        var job = NewJob();
        job.AlertOnFailure = false;
        job.AlertResultOperator = AlertMonitorOperator.NotContains;
        job.AlertResultValue = "OK";

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, status, 100, false));
        Assert.False(job.AlertActive);
    }

    /// <summary>
    ///     没有配置结果规则时，响应体内容不影响报警
    /// </summary>
    [Fact]
    public void NoRuleNeverAlertsOnResponseBody() {
        var job = NewJob();

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "error everywhere"));
        Assert.False(job.AlertActive);
    }

    /// <summary>
    ///     “不包含 OK”规则：响应体里没有 OK 就报警，适合要求必须返回 OK 的接口
    /// </summary>
    [Fact]
    public void NotContainsRuleAlertsWhenExpectedTextIsMissing() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.NotContains;
        job.AlertResultValue = "OK";

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "{\"msg\":\"OK\"}"));
        Assert.Equal(
            ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "{\"msg\":\"busy\"}")
        );
    }

    /// <summary>
    ///     数字规则遇到非数字响应体时视为取不到有效值，按命中报警，而不是静默放过
    /// </summary>
    [Fact]
    public void NumberRuleAlertsWhenBodyIsNotNumeric() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.GreaterThan;
        job.AlertResultValueType = AlertMonitorValueType.Number;
        job.AlertResultValue = "100";

        var text = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 100, string.Empty, true, "{\"count\":5}");

        Assert.NotNull(text);
        Assert.Contains("Result: Value is not a number", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     数字规则：响应体整体是数字，大于阈值时报警；去掉首尾空白后解析
    /// </summary>
    [Fact]
    public void NumberRuleComparesWholeBody() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.GreaterThan;
        job.AlertResultValueType = AlertMonitorValueType.Number;
        job.AlertResultValue = "100";

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, " 100\n"));
        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, " 101\n"));
    }

    /// <summary>
    ///     通知里带上触发报警的响应体，超过 500 个字符时截断；解除通知不带响应体
    /// </summary>
    [Fact]
    public void RaisedTextIncludesTruncatedBodyAndResolvedTextDoesNot() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.Contains;
        job.AlertResultValue = "error";
        var body = "error " + new string('x', 800);

        var raised = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 100, string.Empty, true, body);
        var resolved = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 100, string.Empty, true, "fine");

        Assert.NotNull(raised);
        Assert.Contains("Result: error ", raised, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 600), raised, StringComparison.Ordinal);
        Assert.NotNull(resolved);
        Assert.DoesNotContain("Result:", resolved, StringComparison.Ordinal);
    }

    /// <summary>
    ///     正则规则：匹配 JSON 中的 code 不是 200 时报警
    /// </summary>
    [Fact]
    public void RegexRuleMatchesJsonBody() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.RegexNotMatch;
        job.AlertResultValue = "\"code\"\\s*:\\s*200";

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "{\"code\": 200}"));
        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "{\"code\":500}"));
    }

    /// <summary>
    ///     结果规则与失败、耗时规则是“或”关系，共用同一个报警状态：已因失败报警后，结果规则命中不重复触发，全部恢复后只解除一次
    /// </summary>
    [Fact]
    public void ResultRuleSharesAlertStateWithOtherRules() {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.Contains;
        job.AlertResultValue = "error";

        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 100, true, string.Empty));
        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "an error"));
        Assert.True(job.AlertActive);
        Assert.Equal(ScheduledJobAlertKind.Resolved, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, "all good"));
        Assert.False(job.AlertActive);
    }

    /// <summary>
    ///     对照组：同样的规则在有响应体且不含 OK 时会报警，说明上一个测试里没有报警是因为没有响应体，而不是规则本身没生效
    /// </summary>
    [Fact]
    public void SameRuleAlertsWhenBodyIsPresent() {
        var job = NewJob();
        job.AlertOnFailure = false;
        job.AlertResultOperator = AlertMonitorOperator.NotContains;
        job.AlertResultValue = "OK";

        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 100, true, "busy"));
    }

    /// <summary>
    ///     文本规则：包含 error 即报警，不包含则不报警，默认不区分大小写
    /// </summary>
    /// <param name="body">响应体</param>
    /// <param name="expected">期望的报警动作</param>
    [Theory]
    [InlineData("{\"code\":500,\"msg\":\"Server ERROR\"}", ScheduledJobAlertKind.Raised)]
    [InlineData("{\"code\":200,\"msg\":\"OK\"}", ScheduledJobAlertKind.None)]
    public void TextContainsRuleRaisesWhenMatched(
        string body
        , ScheduledJobAlertKind expected
    ) {
        var job = NewJob();
        job.AlertResultOperator = AlertMonitorOperator.Contains;
        job.AlertResultValue = "error";

        Assert.Equal(expected, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true, body));
    }

    /// <summary>
    ///     创建一个已启用报警、只关心结果规则的作业
    /// </summary>
    /// <returns>计划作业</returns>
    private static ScheduledJob NewJob() {
        return new ScheduledJob { Name = "result", CronExpression = "* * * * *", RequestUrl = "http://localhost/", AlertEnabled = true };
    }
}