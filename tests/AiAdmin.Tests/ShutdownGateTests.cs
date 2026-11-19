using AiAdmin.Api.Middleware;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证安全停机闸门：关闸拒绝新请求、在途计数阻塞空闲判定、稳定窗口、取消停机，以及 Track 对后台工作的登记
/// </summary>
[Collection("ShutdownGate")]
public sealed class ShutdownGateTests : IDisposable
{
    /// <summary>
    ///     闸门关闭后普通请求返回 503，停机管理接口仍放行
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ClosedGateRejectsNewRequestsExceptShutdownEndpointsAsync() {
        var reached = false;
        var middleware = new ShutdownGateMiddleware(_ =>
            {
                reached = true;
                return Task.CompletedTask;
            }
        );
        ShutdownGate.Close();

        var blocked = new DefaultHttpContext { Request = { Path = "/api/user/list" }, Response = { Body = new MemoryStream() } };
        await middleware.InvokeAsync(blocked).ConfigureAwait(true);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, blocked.Response.StatusCode);
        Assert.False(reached);

        var allowed = new DefaultHttpContext { Request = { Path = "/api/system/shutdown/cancel" } };
        await middleware.InvokeAsync(allowed).ConfigureAwait(true);
        Assert.True(reached);
    }

    /// <summary>
    ///     每个用例结束后重新打开闸门，避免静态状态影响其他测试
    /// </summary>
    public void Dispose() {
        ShutdownGate.Open();
    }

    /// <summary>
    ///     中间件放行期间登记为在途请求，结束后归零；被拒绝的请求也会正确归零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task MiddlewareTracksInFlightRequestsAsync() {
        var duringRequest = -1;
        var middleware = new ShutdownGateMiddleware(_ =>
            {
                duringRequest = ShutdownGate.Snapshot()["Http"];
                return Task.CompletedTask;
            }
        );
        var context = new DefaultHttpContext { Request = { Path = "/api/user/list" } };
        await middleware.InvokeAsync(context).ConfigureAwait(true);
        Assert.Equal(1, duringRequest);
        Assert.Equal(0, ShutdownGate.InFlightTotal);

        ShutdownGate.Close();
        var rejected = new DefaultHttpContext { Request = { Path = "/api/user/list" }, Response = { Body = new MemoryStream() } };
        await middleware.InvokeAsync(rejected).ConfigureAwait(true);
        Assert.Equal(0, ShutdownGate.InFlightTotal);
    }

    /// <summary>
    ///     Track 在返回前同步登记，工作结束后归零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task TrackRegistersSynchronouslyAndReleasesAfterWorkAsync() {
        var release = new SemaphoreSlim(0, 1);
        ShutdownGate.Track(ShutdownActivity.Background, release.WaitAsync);
        Assert.Equal(1, ShutdownGate.Snapshot()["Background"]);

        _ = release.Release();
        await WaitUntilZeroAsync().ConfigureAwait(true);
        release.Dispose();
    }

    /// <summary>
    ///     后台工作抛出异常时租约仍会释放，且异常不会传给调用方
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task TrackReleasesLeaseWhenWorkThrowsAsync() {
        ShutdownGate.Track(ShutdownActivity.Background, () => throw new InvalidOperationException("boom"));
        await WaitUntilZeroAsync().ConfigureAwait(true);
        Assert.Equal(0, ShutdownGate.Snapshot()["Background"]);
    }

    /// <summary>
    ///     有在途工作时不算空闲，归零并保持稳定窗口后才返回 true
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitForIdleBlocksWhileInFlightAndRequiresStableWindowAsync() {
        ShutdownGate.Close();
        var lease = ShutdownGate.Enter(ShutdownActivity.Job);
        var wait = ShutdownGate.WaitForIdleAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        await Task.Delay(400).ConfigureAwait(true);
        Assert.False(wait.IsCompleted);
        Assert.Equal(1, ShutdownGate.Snapshot()["Job"]);

        lease.Dispose();
        var releasedAt = Environment.TickCount64;
        Assert.True(await wait.ConfigureAwait(true));
        Assert.True(Environment.TickCount64 - releasedAt >= 250, "must hold zero for the stable window");
    }

    /// <summary>
    ///     稳定窗口内再次出现在途工作会重新计时，避免两项工作之间的瞬时空窗被误判为空闲
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitForIdleRestartsStableWindowWhenWorkReappearsAsync() {
        ShutdownGate.Close();
        var wait = ShutdownGate.WaitForIdleAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        await Task.Delay(250).ConfigureAwait(true);
        var lease = ShutdownGate.Enter(ShutdownActivity.Http);
        await Task.Delay(400).ConfigureAwait(true);
        Assert.False(wait.IsCompleted);

        lease.Dispose();
        Assert.True(await wait.ConfigureAwait(true));
    }

    /// <summary>
    ///     等待期间取消停机，等待返回 false
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task WaitForIdleReturnsFalseWhenReopenedAsync() {
        ShutdownGate.Close();
        var lease = ShutdownGate.Enter(ShutdownActivity.Job);
        var wait = ShutdownGate.WaitForIdleAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        await Task.Delay(100).ConfigureAwait(true);
        ShutdownGate.Open();
        Assert.False(await wait.ConfigureAwait(true));
        lease.Dispose();
    }

    /// <summary>
    ///     等待在途计数归零，最多 5 秒
    /// </summary>
    /// <returns>异步等待任务</returns>
    private static async Task WaitUntilZeroAsync() {
        var deadline = Environment.TickCount64 + 5000;
        while (ShutdownGate.InFlightTotal != 0 && Environment.TickCount64 < deadline) {
            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.Equal(0, ShutdownGate.InFlightTotal);
    }
}