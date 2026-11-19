using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证计划作业报警判定：失败或超时触发、耗时超限触发、持续异常不重复报警、恢复正常才解除、未启用时不通知，以及提醒用户名规范化
/// </summary>
public sealed class ScheduledJobAlertTests
{
    /// <summary>
    ///     未启用报警时任何状态都不通知，并清除残留的报警状态
    /// </summary>
    [Fact]
    public void DisabledAlertNeverNotifies() {
        var job = NewJob();
        job.AlertEnabled = false;
        job.AlertActive = true;

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 1000, true));
        Assert.False(job.AlertActive);
    }

    /// <summary>
    ///     耗时超过设定秒数才触发，刚好等于不触发，设为 0 时不按耗时触发
    /// </summary>
    [Fact]
    public void DurationThresholdRaisesAlert() {
        var job = NewJob();
        job.AlertOverSeconds = 5;

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 5000, true));
        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 5001, true));

        var noLimit = NewJob();
        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(noLimit, ScheduledJobStatus.Success, 999_999, true));
    }

    /// <summary>
    ///     关闭失败报警后，失败不触发；仍可按耗时触发
    /// </summary>
    [Fact]
    public void FailureAlertCanBeDisabledSeparately() {
        var job = NewJob();
        job.AlertOnFailure = false;
        job.AlertOverSeconds = 10;

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 1000, true));
        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 11_000, true));
    }

    /// <summary>
    ///     执行失败或超时触发报警，成功不触发
    /// </summary>
    /// <param name="status">执行状态</param>
    /// <param name="expected">期望的报警动作</param>
    [Theory]
    [InlineData(ScheduledJobStatus.Failed, ScheduledJobAlertKind.Raised)]
    [InlineData(ScheduledJobStatus.Timeout, ScheduledJobAlertKind.Raised)]
    [InlineData(ScheduledJobStatus.Success, ScheduledJobAlertKind.None)]
    public void FailureOrTimeoutRaisesAlert(
        ScheduledJobStatus status
        , ScheduledJobAlertKind expected
    ) {
        var job = NewJob();

        Assert.Equal(expected, ScheduledJobAlertService.Evaluate(job, status, 1000, true));
        Assert.Equal(expected == ScheduledJobAlertKind.Raised, job.AlertActive);
    }

    /// <summary>
    ///     提醒用户名按空白、逗号、分号拆分，补齐 @ 并去重
    /// </summary>
    /// <param name="input">原始输入</param>
    /// <param name="expected">规范化结果</param>
    [Theory]
    [InlineData("alice", "@alice")]
    [InlineData("@alice  bob,@Bob;carol", "@alice @bob @carol")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void NormalizeMentionsCleansInput(
        string? input
        , string expected
    ) {
        Assert.Equal(expected, ScheduledJobAlertService.NormalizeMentions(input));
    }

    /// <summary>
    ///     通知文本区分报警与解除，报警带错误信息和提醒用户，解除不带错误信息
    /// </summary>
    [Fact]
    public void PrepareBuildsRaisedAndResolvedText() {
        var job = NewJob();
        job.AlertMentions = "@alice @bob";

        var raised = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Failed, 2500, "boom", true);
        Assert.NotNull(raised);
        Assert.StartsWith("\U0001F525 ALERT:", raised, StringComparison.Ordinal);
        Assert.Contains("boom", raised, StringComparison.Ordinal);
        Assert.Contains("2.5s", raised, StringComparison.Ordinal);
        Assert.EndsWith("@alice @bob", raised, StringComparison.Ordinal);

        var resolved = ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 800, "ignored", true);
        Assert.NotNull(resolved);
        Assert.StartsWith("\u2705 RESOLVED:", resolved, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", resolved, StringComparison.Ordinal);
        Assert.Null(ScheduledJobAlertService.Prepare(job, ScheduledJobStatus.Success, 800, string.Empty, true));
    }

    /// <summary>
    ///     持续异常期间不重复报警，恢复正常后只解除一次
    /// </summary>
    [Fact]
    public void RepeatedFailureDoesNotRepeatAndRecoveryResolvesOnce() {
        var job = NewJob();

        Assert.Equal(ScheduledJobAlertKind.Raised, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 100, true));
        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Failed, 100, true));
        Assert.Equal(ScheduledJobAlertKind.Resolved, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true));
        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Success, 100, true));
        Assert.False(job.AlertActive);
    }

    /// <summary>
    ///     运行中的作业只会触发报警，不会因为未命中条件而解除
    /// </summary>
    [Fact]
    public void RunningJobNeverResolves() {
        var job = NewJob();
        job.AlertActive = true;

        Assert.Equal(ScheduledJobAlertKind.None, ScheduledJobAlertService.Evaluate(job, ScheduledJobStatus.Running, 100, false));
        Assert.True(job.AlertActive);
    }

    /// <summary>
    ///     创建启用报警的作业
    /// </summary>
    /// <returns>计划作业</returns>
    private static ScheduledJob NewJob() {
        return new ScheduledJob { Name = "alert", CronExpression = "* * * * *", RequestUrl = "http://localhost/", AlertEnabled = true };
    }
}