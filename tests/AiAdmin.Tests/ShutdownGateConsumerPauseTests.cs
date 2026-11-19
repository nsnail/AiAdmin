using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证共享队列消费循环在安全停机期间暂停拉取，以及排空超时时能带回拖住排空的在途工作快照
/// </summary>
[Collection("ShutdownGate")]
public sealed class ShutdownGateConsumerPauseTests : IDisposable
{
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(10);

    /// <summary>
    ///     每个用例结束后重新打开闸门，避免静态状态影响其他测试
    /// </summary>
    public void Dispose() {
        ShutdownGate.Open();
    }

    /// <summary>
    ///     在途工作归零时不触发繁忙回调，并正常返回空闲
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitForIdle_DoesNotReportBusyWhenIdleAsync() {
        ShutdownGate.Close();
        var reported = false;
        var idle = await ShutdownGate
            .WaitForIdleAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None, _ => reported = true)
            .WaitAsync(TimeSpan.FromSeconds(2))
            .ConfigureAwait(true);
        Assert.True(idle);
        Assert.False(reported);
    }

    /// <summary>
    ///     排空等待期间有在途工作时回调快照，超时后调用方仍能拿到当时非零的类别，而不是回到 0 之后的快照
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitForIdle_ReportsBusySnapshotWhileWaitingAsync() {
        ShutdownGate.Close();
        Dictionary<string, int>? lastBusy = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var lease = ShutdownGate.Enter(ShutdownActivity.Background);
        try {
            _ = await Assert
                .ThrowsAnyAsync<OperationCanceledException>(() => ShutdownGate.WaitForIdleAsync(
                        TimeSpan.FromMilliseconds(50), timeout.Token, snapshot => lastBusy = snapshot
                    )
                )
                .ConfigureAwait(true);
        }
        finally {
            lease.Dispose();
        }

        Assert.NotNull(lastBusy);
        Assert.Equal(1, lastBusy["Background"]);
        Assert.Equal(0, ShutdownGate.InFlightTotal);
    }

    /// <summary>
    ///     闸门关闭期间一直等待，重新打开（取消停机）后恢复
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitWhileClosed_BlocksUntilGateReopensAsync() {
        ShutdownGate.Close();
        var waiting = ShutdownGate.WaitWhileClosedAsync(_poll, CancellationToken.None);
        await Task.Delay(100).ConfigureAwait(true);
        Assert.False(waiting.IsCompleted);

        ShutdownGate.Open();
        await waiting.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        Assert.True(waiting.IsCompletedSuccessfully);
    }

    /// <summary>
    ///     闸门打开时不等待，立即返回
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitWhileClosed_ReturnsImmediatelyWhenOpenAsync() {
        ShutdownGate.Open();
        await ShutdownGate.WaitWhileClosedAsync(_poll, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        Assert.False(ShutdownGate.IsClosed);
    }

    /// <summary>
    ///     应用停止导致的取消只结束等待，不抛出异常，让消费循环自行退出
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitWhileClosed_StopsQuietlyWhenCancelledAsync() {
        ShutdownGate.Close();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        // ReSharper disable once MethodSupportsCancellation
        await ShutdownGate.WaitWhileClosedAsync(_poll, cts.Token).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        Assert.True(cts.IsCancellationRequested);
    }
}