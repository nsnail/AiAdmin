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
    ///     启动日志消费循环
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>后台任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var batch = new List<ElasticsearchLogEntry>(Math.Max(1, options.Value.BatchSize));
        while (!stoppingToken.IsCancellationRequested) {
            batch.Clear();
            try {
                var first = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
                if (first is null) {
                    continue;
                }

                batch.Add(first);
                while (batch.Count < Math.Max(1, options.Value.BatchSize)) {
                    var entry = await queue.TryDequeueAsync().ConfigureAwait(false);
                    if (entry is null) {
                        break;
                    }

                    batch.Add(entry);
                }

                await writer.WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception exception) {
                _logWriteFailure(logger, exception);
            }

            try {
                await Task.Delay(options.Value.FlushInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
        }
    }
}