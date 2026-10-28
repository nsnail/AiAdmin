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
    private static readonly Action<ILogger, Exception?> _logWriteFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(3401, "ApiLogStorageWriteFailure"), "Failed to write API request logs to the database; the batch will be retried"
    );

    /// <summary>
    ///     持续从队列取出日志批次并写入数据库，直到应用停止
    /// </summary>
    /// <param name="stoppingToken">应用停止令牌</param>
    /// <returns>后台任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            var wrote = false;
            try {
                wrote = await WriteBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception exception) {
                _logWriteFailure(logger, exception);
            }

            // 有数据时立即取下一批，队列为空或写入失败时按间隔等待，避免空转和对故障数据库的连续冲击
            if (wrote) {
                continue;
            }

            try {
                await Task.Delay(options.Value.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
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
        }
        catch {
            // 写入失败（数据库故障、建表失败等）时放回队列头部等待重试，不丢日志
            await queue.RequeueAsync(batch).ConfigureAwait(false);
            throw;
        }

        return true;
    }
}