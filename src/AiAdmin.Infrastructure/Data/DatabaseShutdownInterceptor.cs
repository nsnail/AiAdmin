using System.Data.Common;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiAdmin.Api.Data;

/// <summary>
///     把数据库连接的打开到关闭区间登记为安全停机的在途工作。
///     一个事务一定占用一个连接，因此以连接生命周期计数既覆盖了命令执行，也覆盖了事务期间命令之间的空档，
///     且不会像事务回调那样在“未提交就释放”或“提交失败”时漏掉结束事件而泄漏计数
/// </summary>
public sealed class DatabaseShutdownInterceptor : DbConnectionInterceptor
{
    /// <summary>
    ///     连接到其租约的映射；关闭或释放时取出并结束登记，保证同一连接只扣减一次
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DbConnection, ShutdownLease> _leases = new();

    /// <summary>
    ///     同步打开连接后登记在途工作
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="eventData">连接事件数据</param>
    public override void ConnectionOpened(
        DbConnection connection
        , ConnectionEndEventData eventData
    ) {
        Register(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <summary>
    ///     异步打开连接后登记在途工作
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="eventData">连接事件数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public override Task ConnectionOpenedAsync(
        DbConnection connection
        , ConnectionEndEventData eventData
        , CancellationToken cancellationToken = default
    ) {
        Register(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    /// <summary>
    ///     连接即将同步关闭时结束登记
    /// </summary>
    /// <param name="connection">同步关闭的数据库连接</param>
    /// <param name="eventData">同步关闭的事件数据</param>
    /// <param name="result">同步关闭的拦截结果</param>
    /// <returns>同步关闭后继续执行的拦截结果</returns>
    public override InterceptionResult ConnectionClosing(
        DbConnection connection
        , ConnectionEventData eventData
        , InterceptionResult result
    ) {
        Unregister(connection);
        return base.ConnectionClosing(connection, eventData, result);
    }

    /// <summary>
    ///     连接即将异步关闭时结束登记
    /// </summary>
    /// <param name="connection">异步关闭的数据库连接</param>
    /// <param name="eventData">异步关闭的事件数据</param>
    /// <param name="result">异步关闭的拦截结果</param>
    /// <returns>异步关闭后继续执行的拦截结果</returns>
    public override ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection
        , ConnectionEventData eventData
        , InterceptionResult result
    ) {
        Unregister(connection);
        return base.ConnectionClosingAsync(connection, eventData, result);
    }

    /// <summary>
    ///     连接被释放时兜底结束登记，覆盖未经过 Close 直接释放的路径
    /// </summary>
    /// <param name="connection">同步释放的数据库连接</param>
    /// <param name="eventData">同步释放的事件数据</param>
    /// <param name="result">同步释放的拦截结果</param>
    /// <returns>同步释放后继续执行的拦截结果</returns>
    public override InterceptionResult ConnectionDisposing(
        DbConnection connection
        , ConnectionEventData eventData
        , InterceptionResult result
    ) {
        Unregister(connection);
        return base.ConnectionDisposing(connection, eventData, result);
    }

    /// <summary>
    ///     连接被异步释放时兜底结束登记
    /// </summary>
    /// <param name="connection">异步释放的数据库连接</param>
    /// <param name="eventData">异步释放的事件数据</param>
    /// <param name="result">异步释放的拦截结果</param>
    /// <returns>异步释放后继续执行的拦截结果</returns>
    public override ValueTask<InterceptionResult> ConnectionDisposingAsync(
        DbConnection connection
        , ConnectionEventData eventData
        , InterceptionResult result
    ) {
        Unregister(connection);
        return base.ConnectionDisposingAsync(connection, eventData, result);
    }

    /// <summary>
    ///     连接失败时结束登记，避免打开过程中异常导致计数泄漏
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="eventData">连接错误事件数据</param>
    public override void ConnectionFailed(
        DbConnection connection
        , ConnectionErrorEventData eventData
    ) {
        Unregister(connection);
        base.ConnectionFailed(connection, eventData);
    }

    /// <summary>
    ///     登记连接，同一连接重复打开时只保留一份租约
    /// </summary>
    /// <param name="connection">数据库连接</param>
    private void Register(DbConnection connection) {
        var lease = ShutdownGate.Enter(ShutdownActivity.Database);
        if (!_leases.TryAdd(connection, lease)) {
            lease.Dispose();
        }
    }

    /// <summary>
    ///     结束连接的登记，未登记时忽略
    /// </summary>
    /// <param name="connection">数据库连接</param>
    private void Unregister(DbConnection connection) {
        if (_leases.TryRemove(connection, out var lease)) {
            lease.Dispose();
        }
    }
}