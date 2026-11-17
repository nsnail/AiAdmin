using AiAdmin.Api.Services;
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
    private static readonly Action<ILogger, Exception?> _logDequeueFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(3102, "ElasticsearchDequeueFailure"), "Failed to read the Elasticsearch log queue; the worker will retry"
    );

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
        var first = await TryReadAsync(() => queue.DequeueAsync(stoppingToken), stoppingToken).ConfigureAwait(false);
        if (first is null) {
            return false;
        }

        batch.Add(first);
        var batchSize = Math.Max(1, options.Value.BatchSize);
        while (batch.Count < batchSize) {
            // 读取中途失败时保留已取出的日志先写入，避免丢失
            var entry = await TryReadAsync(queue.TryDequeueAsync, stoppingToken).ConfigureAwait(false);
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
            // 安全停机闸门关闭后不再取新批次，积压留在共享队列由其他实例消费，否则持续取到数据会让在途工作无法归零
            await ShutdownGate.WaitWhileClosedAsync(options.Value.FlushInterval, stoppingToken).ConfigureAwait(false);
            batch.Clear();
            var hasData = await DequeueBatchAsync(batch, stoppingToken).ConfigureAwait(false);
            if (!hasData) {
                continue;
            }

            // 只在已取到数据后登记，空队列时的无限轮询不属于在途工作；批次写完即释放
            var lease = ShutdownGate.Enter(ShutdownActivity.Background);
            try {
                await writer.WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception exception) {
                _logWriteFailure(logger, exception);
            }
            finally {
                lease.Dispose();
            }

            await DelayBeforeNextBatchAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     读取队列并吞掉非取消异常：Redis 超时等短暂故障只记录日志并等待下一轮，不让异常逃出 Worker 导致宿主被停止
    /// </summary>
    /// <param name="read">读取队列的操作</param>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>读取到的日志，队列为空或读取失败时返回 null</returns>
    private async Task<ElasticsearchLogEntry?> TryReadAsync(
        Func<Task<ElasticsearchLogEntry?>> read
        , CancellationToken stoppingToken
    ) {
        try {
            return await read().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            return null;
        }
        catch (Exception exception) {
            _logDequeueFailure(logger, exception);
            await DelayBeforeNextBatchAsync(stoppingToken).ConfigureAwait(false);
            return null;
        }
    }
}