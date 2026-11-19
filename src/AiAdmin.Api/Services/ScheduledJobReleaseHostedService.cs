using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     定期释放运行时间超过作业超时设置的计划作业
/// </summary>
/// <param name="scopeFactory">服务作用域工厂</param>
/// <param name="lockService">计划作业分布式锁服务</param>
/// <param name="logger">日志记录器</param>
public sealed class ScheduledJobReleaseHostedService(
    IServiceScopeFactory scopeFactory
    , ScheduledJobLockService lockService
    , ILogger<ScheduledJobReleaseHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan _lockWaitTimeout = TimeSpan.FromSeconds(2);

    private static readonly Action<ILogger, Exception?> _logReleaseLoopError = LoggerMessage.Define(
        LogLevel.Error, new EventId(3002, "ScheduledJobReleaseLoopError"), "Scheduled job release loop failed"
    );

    private static readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     启动计划作业超时释放循环
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    /// <returns>异步执行任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await ReleaseTimedOutJobsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) {
                _logReleaseLoopError(logger, exception);
            }

            await Task.Delay(_scanInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     判断运行中的作业是否已超过报警耗时且尚未报警
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="now">当前时间</param>
    /// <returns>应触发超时报警返回 true</returns>
    private static bool IsRunningTooLong(
        ScheduledJob job
        , DateTime now
    ) {
        return job is { AlertEnabled: true, AlertActive: false, AlertOverSeconds: > 0, Status: ScheduledJobStatus.Running, LastTriggeredAt: not null }
               && job.LastTriggeredAt.Value.AddSeconds(job.AlertOverSeconds) <= now;
    }

    /// <summary>
    ///     在分布式锁保护下复核并触发运行超过报警耗时的作业报警，作业仍在运行，只触发不解除
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="alerts">报警服务</param>
    /// <param name="job">运行中的作业</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步处理任务</returns>
    private async Task AlertLongRunningAsync(
        AppDbContext db
        , ScheduledJobAlertService alerts
        , ScheduledJob job
        , CancellationToken cancellationToken
    ) {
        string? alertText;
        await using (var jobLock = await lockService.TryAcquireAsync(job.Id, _lockWaitTimeout, cancellationToken).ConfigureAwait(false)) {
            if (jobLock is null) {
                return;
            }

            await db.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (!IsRunningTooLong(job, now)) {
                return;
            }

            alertText = ScheduledJobAlertService.Prepare(
                job, ScheduledJobStatus.Running, (long)(now - job.LastTriggeredAt!.Value).TotalMilliseconds, string.Empty, false
            );
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (alertText is not null) {
            await alerts.NotifyAsync(alertText, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     在分布式锁保护下把运行超过超时设置的作业标记为超时，并按需发送报警
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="alerts">报警服务</param>
    /// <param name="job">待释放的作业</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步处理任务</returns>
    private async Task ReleaseOneAsync(
        AppDbContext db
        , ScheduledJobAlertService alerts
        , ScheduledJob job
        , CancellationToken cancellationToken
    ) {
        string? alertText;
        await using (var jobLock = await lockService.TryAcquireAsync(job.Id, _lockWaitTimeout, cancellationToken).ConfigureAwait(false)) {
            if (jobLock is null) {
                return;
            }

            await db.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (job.Status != ScheduledJobStatus.Running
                || !job.LastTriggeredAt.HasValue
                || job.LastTriggeredAt.Value.AddSeconds(job.TimeoutSeconds) > now) {
                return;
            }

            job.Status = ScheduledJobStatus.Timeout;
            job.LastFinishedAt = now;
            job.LastError = "Job execution exceeded timeout";
            alertText = ScheduledJobAlertService.Prepare(
                job, ScheduledJobStatus.Timeout, (long)(now - job.LastTriggeredAt.Value).TotalMilliseconds, job.LastError, true
            );
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (alertText is not null) {
            await alerts.NotifyAsync(alertText, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     扫描并释放已超过运行超时时间的计划作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步处理任务</returns>
    private async Task ReleaseTimedOutJobsAsync(CancellationToken cancellationToken) {
        // 安全停机闸门关闭后跳过本轮，超时作业由另一个实例通过分布式锁接手
        using var lease = ShutdownGate.Enter(ShutdownActivity.Job);
        if (ShutdownGate.IsClosed) {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var jobs = await db
            .ScheduledJobs.Where(x => x.Status == ScheduledJobStatus.Running && x.LastTriggeredAt.HasValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var alerts = scope.ServiceProvider.GetRequiredService<ScheduledJobAlertService>();
        foreach (var job in jobs.Where(x => x.LastTriggeredAt!.Value.AddSeconds(x.TimeoutSeconds) <= now)) {
            await ReleaseOneAsync(db, alerts, job, cancellationToken).ConfigureAwait(false);
        }

        foreach (var job in jobs.Where(x => IsRunningTooLong(x, now))) {
            await AlertLongRunningAsync(db, alerts, job, cancellationToken).ConfigureAwait(false);
        }
    }
}