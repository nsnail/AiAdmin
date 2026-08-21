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
    ///     扫描并释放已超过运行超时时间的计划作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步处理任务</returns>
    private async Task ReleaseTimedOutJobsAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.Now;
        var jobs = await db
            .ScheduledJobs.Where(x => x.Status == ScheduledJobStatus.Running && x.LastTriggeredAt.HasValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var job in jobs.Where(x => x.LastTriggeredAt!.Value.AddSeconds(x.TimeoutSeconds) <= now)) {
            await using var jobLock = await lockService.TryAcquireAsync(job.Id, _lockWaitTimeout, cancellationToken).ConfigureAwait(false);
            if (jobLock is null) {
                continue;
            }

            await db.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            now = DateTime.Now;
            if (job.Status != ScheduledJobStatus.Running
                || !job.LastTriggeredAt.HasValue
                || job.LastTriggeredAt.Value.AddSeconds(job.TimeoutSeconds) > now) {
                continue;
            }

            job.Status = ScheduledJobStatus.Timeout;
            job.LastFinishedAt = now;
            job.LastError = "Job execution exceeded timeout";
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}