using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     批量消费 Redis 日志队列并写入 Elasticsearch 的后台任务
/// </summary>
/// <param name="queue">Redis 日志队列</param>
/// <param name="writer">Elasticsearch 日志写入器</param>
/// <param name="options">日志输出配置</param>
/// <param name="logger">后台任务运行日志记录器</param>
public sealed class ElasticsearchLogBackgroundService(
    ElasticsearchLogQueue queue
    , ElasticsearchLogWriter writer
    , IOptions<ElasticsearchLogOptions> options
    , ILogger<ElasticsearchLogBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> _logWriteFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(3101, "ElasticsearchWriteFailure"), "Failed to write Elasticsearch logs; the current batch will be discarded"
    );

    /// <summary>
    ///     启动并行消费 Worker
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>后台任务</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) {
        var workerCount = Math.Max(1, options.Value.WorkerCount);
        return Task.WhenAll(Enumerable.Range(0, workerCount).Select(_ => RunWorkerAsync(stoppingToken)));
    }

    /// <summary>
    ///     等待下一次批量消费
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>异步任务</returns>
    private async Task DelayBeforeNextBatchAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(options.Value.FlushInterval, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Ignore
        }
    }

    /// <summary>
    ///     从队列中读取达到配置上限的一批日志
    /// </summary>
    /// <param name="batch">批次缓冲区</param>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>是否读取到日志</returns>
    private async Task<bool> DequeueBatchAsync(
        List<ElasticsearchLogEntry> batch
        , CancellationToken stoppingToken
    ) {
        var first = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
        if (first is null) {
            return false;
        }

        batch.Add(first);
        var batchSize = Math.Max(1, options.Value.BatchSize);
        while (batch.Count < batchSize) {
            var entry = await queue.TryDequeueAsync().ConfigureAwait(false);
            if (entry is null) {
                break;
            }

            batch.Add(entry);
        }

        return true;
    }

    /// <summary>
    ///     单个消费 Worker 循环，持续从队列取出日志批次并写入 Elasticsearch
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>异步任务</returns>
    private async Task RunWorkerAsync(CancellationToken stoppingToken) {
        var batch = new List<ElasticsearchLogEntry>(Math.Max(1, options.Value.BatchSize));
        while (!stoppingToken.IsCancellationRequested) {
            batch.Clear();
            var hasData = await DequeueBatchAsync(batch, stoppingToken).ConfigureAwait(false);
            if (!hasData) {
                continue;
            }

            try {
                await writer.WriteAsync(batch, stoppingToken).ConfigureAwait(false);
                await DelayBeforeNextBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception exception) {
                _logWriteFailure(logger, exception);
                await DelayBeforeNextBatchAsync(stoppingToken).ConfigureAwait(false);
            }
        }
    }
}