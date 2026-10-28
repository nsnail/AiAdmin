using System.Text.Json;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using StackExchange.Redis;

namespace AiAdmin.Api.Logging;

/// <summary>
///     使用 Redis List 缓存待写入数据库的 API 请求日志，多个实例共用同一队列，实例重启不丢数据
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="options">请求日志入库配置</param>
public sealed class ApiLogStorageQueue(IConnectionMultiplexer connectionMultiplexer, ApiLogStorageOptions options)
{
    private const string _ENQUEUE_SCRIPT = "redis.call('rpush', KEYS[1], ARGV[1]); redis.call('ltrim', KEYS[1], -tonumber(ARGV[2]), -1); return 1";
    private readonly int _capacity = Math.Max(1000, options.QueueCapacity);
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly RedisKey _key = options.QueueKey;

    /// <summary>
    ///     从队列头部取出最多指定数量的日志，队列为空时返回空集合
    /// </summary>
    /// <param name="count">最大数量</param>
    /// <returns>取出的日志</returns>
    public async Task<IReadOnlyList<ApiRequestLog>> DequeueBatchAsync(int count) {
        var values = await _database.ListLeftPopAsync(_key, count).ConfigureAwait(false);
        if (values.Length == 0) {
            return [];
        }

        var logs = new List<ApiRequestLog>(values.Length);
        logs.AddRange(
            values
                .Where(x => !x.IsNullOrEmpty)
                .Select(value => JsonSerializer.Deserialize<ApiRequestLog>(value.ToString(), JsonParsing.Options))
                .OfType<ApiRequestLog>()
        );

        return logs;
    }

    /// <summary>
    ///     将请求日志序列化后写入 Redis 队列，超出容量时丢弃最旧的日志
    /// </summary>
    /// <param name="log">请求日志</param>
    /// <returns>异步任务</returns>
    public async Task EnqueueAsync(ApiRequestLog log) {
        var payload = JsonSerializer.Serialize(log);
        _ = await _database.ScriptEvaluateAsync(_ENQUEUE_SCRIPT, [_key], [payload, _capacity]).ConfigureAwait(false);
    }

    /// <summary>
    ///     将写入失败的日志放回队列头部，保持原有顺序，等待下一轮重试
    /// </summary>
    /// <param name="logs">写入失败的日志</param>
    /// <returns>异步任务</returns>
    public async Task RequeueAsync(IReadOnlyList<ApiRequestLog> logs) {
        if (logs.Count == 0) {
            return;
        }

        // 逆序逐条放到头部，最终顺序与取出时一致
        var payloads = logs.Reverse().Select(x => (RedisValue)JsonSerializer.Serialize(x)).ToArray();
        _ = await _database.ListLeftPushAsync(_key, payloads).ConfigureAwait(false);
    }
}