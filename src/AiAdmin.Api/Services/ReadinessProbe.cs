using System.Data.Common;
using System.Diagnostics;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     就绪探测：真正访问一次数据库和 Redis 并返回各自耗时，供滚动发布判断新实例的连接池是否已预热；
///     与只返回固定内容的 /health 不同，/health 只说明进程存活，不说明依赖可用
/// </summary>
public static class ReadinessProbe
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     执行就绪探测
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <param name="redis">Redis 连接复用器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>就绪结果，数据库和 Redis 都在超时内应答才算就绪</returns>
    public static async Task<ReadinessResult> CheckAsync(
        AppDbContext db
        , IConnectionMultiplexer redis
        , CancellationToken cancellationToken
    ) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var (databaseOk, databaseMilliseconds) = await MeasureAsync(
                async token => _ = await db.Database.ExecuteSqlRawAsync("SELECT 1", token).ConfigureAwait(false), timeout.Token
            )
            .ConfigureAwait(false);
        var (cacheOk, cacheMilliseconds) = await MeasureAsync(
                async token => _ = await redis.GetDatabase().PingAsync().WaitAsync(token).ConfigureAwait(false), timeout.Token
            )
            .ConfigureAwait(false);
        return new ReadinessResult(databaseOk && cacheOk, databaseMilliseconds, cacheMilliseconds);
    }

    /// <summary>
    ///     执行一次依赖访问并计时，访问失败或超时视为不可用
    /// </summary>
    /// <param name="action">依赖访问，接收超时取消令牌</param>
    /// <param name="cancellationToken">超时取消令牌，由调用方持有并保证在访问结束前不被释放</param>
    /// <returns>是否成功和耗时毫秒数</returns>
    private static async Task<(bool Ok, long Milliseconds)> MeasureAsync(
        Func<CancellationToken, Task> action
        , CancellationToken cancellationToken
    ) {
        var stopwatch = Stopwatch.StartNew();
        try {
            await action(cancellationToken).ConfigureAwait(false);
            return (true, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is DbException or RedisException or OperationCanceledException or InvalidOperationException) {
            return (false, stopwatch.ElapsedMilliseconds);
        }
    }
}