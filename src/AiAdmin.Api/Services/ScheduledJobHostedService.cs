using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     计划作业后台调度器，按秒检查到期作业并串行保护同一作业
/// </summary>
/// <param name="scopeFactory">服务作用域工厂</param>
/// <param name="externalHttpRequestService">外部 HTTP 请求服务</param>
/// <param name="lockService">计划作业分布式锁服务</param>
/// <param name="logger">日志记录器</param>
public sealed class ScheduledJobHostedService(
    IServiceScopeFactory scopeFactory
    , ExternalHttpRequestService externalHttpRequestService
    , ScheduledJobLockService lockService
    , ILogger<ScheduledJobHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan _completionLockWaitTimeout = TimeSpan.FromSeconds(35);

    private static readonly Action<ILogger, Exception?> _logSchedulingLoopError = LoggerMessage.Define(
        LogLevel.Error, new EventId(3001, "ScheduledJobLoopError"), "Scheduled job loop failed"
    );

    private static readonly Regex _placeholder = new(
        @"\{\{([^{}]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)
    );

    /// <summary>
    ///     后台循环入口
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    /// <returns>异步执行任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) {
                _logSchedulingLoopError(logger, exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     使用字典值替换文本中的占位符
    /// </summary>
    /// <param name="value">待处理文本</param>
    /// <param name="values">占位符值集合</param>
    /// <returns>替换后的文本</returns>
    private static string Resolve(
        string value
        , Dictionary<string, string> values
    ) {
        return _placeholder.Replace(value, match => values.TryGetValue(match.Groups[1].Value.Trim(), out var result) ? result : match.Value);
    }

    /// <summary>
    ///     执行指定计划作业并保存执行结果
    /// </summary>
    /// <param name="jobId">作业编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步执行任务</returns>
    /// <exception cref="InvalidOperationException">无法获取计划作业锁时引发</exception>
    private async Task ExecuteJobAsync(
        long jobId
        , CancellationToken cancellationToken
    ) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.ScheduledJobs.FindAsync([jobId], cancellationToken).ConfigureAwait(false);
        if (job is null) {
            return;
        }

        var triggeredAt = job.LastTriggeredAt;
        var dictionarySnapshotService = scope.ServiceProvider.GetRequiredService<DictionarySnapshotService>();
        var placeholderItems = await dictionarySnapshotService
            .GetItemsAsync(DictionarySnapshotService.SCHEDULED_JOB_PLACEHOLDERS_CODE, cancellationToken)
            .ConfigureAwait(false);
        var values = placeholderItems.Where(x => x.IsEnabled).ToDictionary(x => x.Label, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var url = Resolve(job.RequestUrl, values);
        var headers = Resolve(job.RequestHeadersJson, values);
        var body = Resolve(job.RequestBody, values);
        var execution = new ScheduledJobExecution
        {
            ScheduledJobId = job.Id
            , StartedAt = DateTime.Now
            , RequestUrl = url
            , RequestMethod = job.RequestMethod
            , RequestHeaders = headers
            , RequestBody = body
        };
        _ = await db.ScheduledJobExecutions.AddAsync(execution, cancellationToken).ConfigureAwait(false);
        try {
            using var request = new HttpRequestMessage(new HttpMethod(job.RequestMethod), url);
            if (!string.IsNullOrWhiteSpace(body) && job.RequestMethod is not "GET" and not "HEAD") {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var headerDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(headers) ? "{}" : headers);
            foreach (var item in headerDoc.RootElement.EnumerateObject()) {
                _ = request.Headers.TryAddWithoutValidation(item.Name, item.Value.GetString() ?? item.Value.ToString());
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(job.TimeoutSeconds, 1, 86400)));
            var response = await externalHttpRequestService
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            execution.RequestHeaders = response.RequestHeaders;
            execution.RequestBody = response.RequestBody;
            execution.ResponseStatusCode = response.StatusCode;
            execution.ResponseHeaders = response.ResponseHeaders;
            execution.ResponseBody = response.ResponseBody;
            execution.Status = response.StatusCode is >= 200 and < 300 ? ScheduledJobStatus.Success : ScheduledJobStatus.Failed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            execution.Status = ScheduledJobStatus.Timeout;
            execution.ErrorMessage = "Request timed out";
        }
        catch (Exception exception) {
            execution.Status = ScheduledJobStatus.Failed;
            execution.ErrorMessage = exception.Message;
        }

        execution.FinishedAt = DateTime.Now;
        await using var jobLock = await lockService.TryAcquireAsync(job.Id, _completionLockWaitTimeout, CancellationToken.None).ConfigureAwait(false)
                                  ?? throw new InvalidOperationException("Unable to acquire scheduled job lock");

        await db.Entry(job).ReloadAsync(CancellationToken.None).ConfigureAwait(false);
        if (job.Status == ScheduledJobStatus.Running && job.LastTriggeredAt == triggeredAt) {
            job.Status = execution.Status;
            job.LastFinishedAt = execution.FinishedAt;
            job.LastError = execution.ErrorMessage;
        }

        _ = await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     检查并触发当前时间到期的作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步处理任务</returns>
    private async Task TickAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.Now;
        var jobs = await db
            .ScheduledJobs.Where(x => x.IsEnabled && x.Status != ScheduledJobStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var job in jobs.Where(x =>
                     CronMatcher.IsDue(x.CronExpression, now)
                     && (!x.LastTriggeredAt.HasValue || !CronMatcher.IsSameTriggerWindow(x.CronExpression, x.LastTriggeredAt.Value, now))
                 )) {
            await using (var jobLock = await lockService.TryAcquireAsync(job.Id, TimeSpan.Zero, cancellationToken).ConfigureAwait(false)) {
                if (jobLock is null) {
                    continue;
                }

                await db.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
                now = DateTime.Now;
                if (!job.IsEnabled
                    || job.Status == ScheduledJobStatus.Running
                    || !CronMatcher.IsDue(job.CronExpression, now)
                    || (job.LastTriggeredAt.HasValue && CronMatcher.IsSameTriggerWindow(job.CronExpression, job.LastTriggeredAt.Value, now))) {
                    continue;
                }

                job.Status = ScheduledJobStatus.Running;
                job.LastTriggeredAt = now;
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _ = ExecuteJobAsync(job.Id, cancellationToken);
        }
    }
}