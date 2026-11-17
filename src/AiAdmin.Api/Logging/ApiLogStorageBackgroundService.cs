using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     批量消费 Redis 请求日志队列并写入按天分表的后台服务
/// </summary>
/// <param name="queue">请求日志队列</param>
/// <param name="tables">请求日志分表服务</param>
/// <param name="options">请求日志入库配置</param>
/// <param name="logger">后台服务运行日志记录器</param>
public sealed class ApiLogStorageBackgroundService(
    ApiLogStorageQueue queue
    , ApiRequestLogTableService tables
    , IOptions<ApiLogStorageOptions> options
    , ILogger<ApiLogStorageBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> _logDeadLettered = LoggerMessage.Define<int>(
        LogLevel.Error, new EventId(3402, "ApiLogStorageDeadLettered")
        , "Moved {Count} API request logs that repeatedly failed to the dead letter queue"
    );

    private static readonly Action<ILogger, Exception?> _logWriteFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(3401, "ApiLogStorageWriteFailure"), "Failed to write API request logs to the database; the batch will be retried"
    );

    private int _failureCount;

    /// <summary>
    ///     持续从队列取出日志批次并写入数据库，直到应用停止
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>后台任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            // 安全停机闸门关闭后不再取新批次，积压留在共享队列由其他实例消费，否则持续取到数据会让在途工作无法归零
            await ShutdownGate.WaitWhileClosedAsync(options.Value.PollInterval, stoppingToken).ConfigureAwait(false);

            // 有数据时立即取下一批，队列为空或写入失败时按间隔等待，避免空转和对故障数据库的连续冲击
            if (!await TryWriteBatchAsync(stoppingToken).ConfigureAwait(false)) {
                await DelayBeforeNextPollAsync(stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     等待下一次轮询，应用停止导致的取消直接结束等待
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>异步任务</returns>
    private async Task DelayBeforeNextPollAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(GetDelay(), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Ignore
        }
    }

    /// <summary>
    ///     计算下一次轮询前的等待时间：无失败时为轮询间隔，连续失败时从轮询间隔开始逐次翻倍，不超过重试等待上限
    /// </summary>
    /// <returns>等待时间</returns>
    private TimeSpan GetDelay() {
        var interval = options.Value.PollInterval;
        if (_failureCount == 0) {
            return interval;
        }

        var factor = Math.Pow(2, Math.Min(_failureCount - 1, 16));
        var delay = TimeSpan.FromTicks((long)Math.Min(interval.Ticks * factor, options.Value.MaxRetryDelay.Ticks));
        return delay < interval ? interval : delay;
    }

    /// <summary>
    ///     处理写入失败：未达到重试上限时整批放回队列头部等待重试；达到上限后逐条写入，写入成功的丢弃，仍然失败的转入死信队列，避免坏数据永久堵住队列
    /// </summary>
    /// <param name="batch">写入失败的批次</param>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>异步任务</returns>
    private async Task HandleFailureAsync(
        IReadOnlyList<ApiRequestLog> batch
        , CancellationToken stoppingToken
    ) {
        _failureCount++;
        if (_failureCount < Math.Max(1, options.Value.MaxRetries)) {
            await queue.RequeueAsync(batch).ConfigureAwait(false);
            return;
        }

        var failed = new List<ApiRequestLog>();
        var retry = new List<ApiRequestLog>();
        foreach (var log in batch) {
            try {
                _ = await tables.InsertAsync([log], stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                failed.Add(log);
            }
            catch {
                retry.Add(log);
            }
        }

        if (failed.Count > 0) {
            await queue.DeadLetterAsync(failed).ConfigureAwait(false);
            _logDeadLettered(logger, failed.Count, null);
        }

        // 逐条写入时被取消的日志放回队列，其余已处理；有任何一条成功说明数据库可用，清零失败计数
        await queue.RequeueAsync(retry).ConfigureAwait(false);
        if (failed.Count < batch.Count) {
            _failureCount = 0;
        }
    }

    /// <summary>
    ///     写入一批日志，写入失败时记录错误并视为未写入，应用停止导致的取消不算失败
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>本批取到并写入日志时为 true，队列为空、写入失败或应用已停止时为 false</returns>
    private async Task<bool> TryWriteBatchAsync(CancellationToken stoppingToken) {
        try {
            return await WriteBatchAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            return false;
        }
        catch (Exception exception) {
            _logWriteFailure(logger, exception);
            return false;
        }
    }

    /// <summary>
    ///     取出一批日志写入分表，写入失败时放回队列并向上抛出
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>本批取到并写入日志时为 true，队列为空时为 false</returns>
    private async Task<bool> WriteBatchAsync(CancellationToken stoppingToken) {
        var batch = await queue.DequeueBatchAsync(Math.Max(1, options.Value.BatchSize)).ConfigureAwait(false);
        if (batch.Count == 0) {
            return false;
        }

        // 取到数据后才登记在途工作，空队列轮询不阻塞安全停机；批次写完即释放
        using var lease = ShutdownGate.Enter(ShutdownActivity.Background);
        try {
            _ = await tables.InsertAsync(batch, stoppingToken).ConfigureAwait(false);
            _failureCount = 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            await HandleFailureAsync(batch, stoppingToken).ConfigureAwait(false);
            throw;
        }
        catch {
            // 应用停止导致的取消：日志放回队列，下次启动继续
            await queue.RequeueAsync(batch).ConfigureAwait(false);
            throw;
        }

        return true;
    }
}