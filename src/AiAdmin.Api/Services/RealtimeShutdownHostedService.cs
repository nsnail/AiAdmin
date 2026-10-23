namespace AiAdmin.Api.Services;

/// <summary>
///     管理实时连接的生命周期：启动时订阅跨实例推送总线；进程收到停机信号时关闭安全停机闸门并主动关闭实时长连接，避免 Kestrel 排空请求时被存量 WebSocket 拖满停机超时
/// </summary>
/// <param name="realtime">实时连接管理器</param>
public sealed class RealtimeShutdownHostedService(RealtimeConnectionManager realtime) : IHostedService
{
    /// <summary>
    ///     启动时让连接管理器订阅跨实例推送总线
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已完成的任务</returns>
    public Task StartAsync(CancellationToken cancellationToken) {
        realtime.Start();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     停止时先关闸再关闭所有 WebSocket；托管服务按注册的逆序停止，本服务注册在 Web 服务器之后，因此先于 Kestrel 排空请求执行
    /// </summary>
    /// <param name="cancellationToken">停机超时令牌</param>
    /// <returns>异步停止任务</returns>
    public Task StopAsync(CancellationToken cancellationToken) {
        ShutdownGate.Close();
        return realtime.CloseAllAsync().WaitAsync(cancellationToken);
    }
}