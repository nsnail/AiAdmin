namespace AiAdmin.Api.Services;

/// <summary>
///     安全停机闸门：控制进程是否继续接收新流量，并按类别统计在途工作数量，在途工作全部归零且保持稳定即认为系统空闲。
///     所有脱离请求生命周期运行的工作（fire-and-forget、后台循环中的单轮处理）都必须通过 <see cref="Enter" /> 或 <see cref="Track" /> 登记，
///     ShutdownGateArchitectureTests 会扫描源码强制检查
/// </summary>
public static class ShutdownGate
{
    private static readonly int[] _inFlight = new int[Enum.GetValues<ShutdownActivity>().Length];
    private static int _closed;

    /// <summary>
    ///     各类别在途工作数量之和
    /// </summary>
    public static int InFlightTotal
    {
        get
        {
            var total = 0;
            for (var i = 0; i < _inFlight.Length; i++) {
                total += Volatile.Read(ref _inFlight[i]);
            }

            return total;
        }
    }

    /// <summary>
    ///     闸门是否已关闭，关闭后只处理存量请求，不再接收新请求
    /// </summary>
    public static bool IsClosed => Volatile.Read(ref _closed) == 1;

    /// <summary>
    ///     关闭闸门，之后新请求会被中间件拒绝
    /// </summary>
    public static void Close() {
        _ = Interlocked.Exchange(ref _closed, 1);
    }

    /// <summary>
    ///     登记一项在途工作，释放返回值时登记结束
    /// </summary>
    /// <param name="activity">在途工作类别</param>
    /// <returns>用于结束登记的租约</returns>
    public static ShutdownLease Enter(ShutdownActivity activity) {
        _ = Interlocked.Increment(ref _inFlight[(int)activity]);
        return new ShutdownLease(activity);
    }

    /// <summary>
    ///     重新打开闸门，恢复接收新流量
    /// </summary>
    public static void Open() {
        _ = Interlocked.Exchange(ref _closed, 0);
    }

    /// <summary>
    ///     读取各类别在途工作数量快照
    /// </summary>
    /// <returns>类别名称到数量的字典</returns>
    public static Dictionary<string, int> Snapshot() {
        return Enum.GetValues<ShutdownActivity>().ToDictionary(x => x.ToString(), x => Volatile.Read(ref _inFlight[(int)x]), StringComparer.Ordinal);
    }

    /// <summary>
    ///     登记并启动一项脱离调用方生命周期的后台工作，登记在返回前同步完成，不存在“已发起但未登记”的空窗；
    ///     工作抛出的异常会被丢弃（与原先的 fire-and-forget 行为一致），需要记录的异常应由 <paramref name="work" /> 自行处理
    /// </summary>
    /// <param name="activity">在途工作类别</param>
    /// <param name="work">后台工作</param>
    public static void Track(
        ShutdownActivity activity
        , Func<Task> work
    ) {
        var lease = Enter(activity);
        _ = RunTrackedAsync(lease, work);
    }

    /// <summary>
    ///     等待在途工作归零，且连续保持 <paramref name="stable" /> 时长，避免恰好落在两项工作之间的瞬时空窗被误判为空闲
    /// </summary>
    /// <param name="stable">要求在途工作连续为零的稳定时长</param>
    /// <param name="cancellationToken">取消令牌，超时由调用方通过它实现</param>
    /// <param name="onBusy">每次发现仍有在途工作时回调，参数为各类别当前数量，可为空</param>
    /// <returns>空闲返回 true；等待期间闸门被重新打开返回 false</returns>
    public static async Task<bool> WaitForIdleAsync(
        TimeSpan stable
        , CancellationToken cancellationToken
        , Action<Dictionary<string, int>>? onBusy = null
    ) {
        var zeroSince = (long?)null;
        var stableMilliseconds = (long)stable.TotalMilliseconds;
        while (IsClosed) {
            if (InFlightTotal == 0) {
                zeroSince ??= Environment.TickCount64;
                if (Environment.TickCount64 - zeroSince.Value >= stableMilliseconds) {
                    return true;
                }
            }
            else {
                zeroSince = null;

                // 有在途工作时把当前快照交给调用方，超时后才取快照会全是 0，无法判断是谁拖住了排空
                onBusy?.Invoke(Snapshot());
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    ///     供共享队列的后台消费循环在每轮拉取前调用：闸门关闭期间不再取新批次，积压数据留在共享队列里由其他存活实例继续消费，
    ///     本进程的在途工作才能归零；闸门重新打开（取消停机）或应用停止时返回
    /// </summary>
    /// <param name="pollInterval">闸门关闭期间的检查间隔</param>
    /// <param name="cancellationToken">应用停止令牌</param>
    /// <returns>异步任务，应用停止导致的取消不抛出异常</returns>
    public static async Task WaitWhileClosedAsync(
        TimeSpan pollInterval
        , CancellationToken cancellationToken
    ) {
        try {
            while (IsClosed) {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // 应用停止，调用方的循环会随即退出
        }
    }

    /// <summary>
    ///     结束一项在途工作登记
    /// </summary>
    /// <param name="activity">在途工作类别</param>
    internal static void Exit(ShutdownActivity activity) {
        _ = Interlocked.Decrement(ref _inFlight[(int)activity]);
    }

    /// <summary>
    ///     执行后台工作并在结束时释放租约
    /// </summary>
    /// <param name="lease">在途工作租约</param>
    /// <param name="work">后台工作</param>
    /// <returns>工作完成的任务</returns>
    private static async Task RunTrackedAsync(
        ShutdownLease lease
        , Func<Task> work
    ) {
        // 异常留在被丢弃的任务里，不影响调用方；using 保证无论成败租约都会释放
        using (lease) {
            await work().ConfigureAwait(false);
        }
    }
}