namespace AiAdmin.Api.Services;

/// <summary>
///     一项在途工作的登记租约，释放时自动从安全停机闸门的计数中扣除
/// </summary>
/// <param name="activity">在途工作类别</param>
public readonly struct ShutdownLease(ShutdownActivity activity) : IDisposable
{
    /// <summary>
    ///     结束登记
    /// </summary>
    public void Dispose() {
        ShutdownGate.Exit(activity);
    }
}