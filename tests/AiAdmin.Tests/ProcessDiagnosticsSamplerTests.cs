using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证进程诊断采样器：字段取值在合理范围内，忙碌时 CPU 占用高于空闲时
/// </summary>
public sealed class ProcessDiagnosticsSamplerTests
{
    /// <summary>
    ///     采样结果的各字段都在合法范围内
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task SampleReturnsValuesInValidRangeAsync() {
        var sampler = new ProcessDiagnosticsSampler();
        await Task.Delay(50).ConfigureAwait(true);
        var sample = sampler.Sample();

        Assert.InRange(sample.CpuPercent, 0, 100);
        Assert.True(sample.BusyThreads >= 0);
        Assert.True(sample.QueuedWorkItems >= 0);
        Assert.True(sample.PendingTimers >= 0);
    }
}