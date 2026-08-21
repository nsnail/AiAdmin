using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StackExchange.Redis;

namespace AiAdmin.Api.Logging;

/// <summary>
///     使用 Redis List 保存待写入 Elasticsearch 的日志队列
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="options">日志输出配置</param>
[SuppressMessage("Design", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue is the established name for this Redis-backed component.")]
public sealed class ElasticsearchLogQueue(IConnectionMultiplexer connectionMultiplexer, ElasticsearchLogOptions options)
{
    private const string EnqueueScript = "redis.call('rpush', KEYS[1], ARGV[1]); redis.call('ltrim', KEYS[1], -tonumber(ARGV[2]), -1); return 1";
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly RedisKey _key = options.QueueKey;
    private readonly int _capacity = Math.Max(100, options.QueueCapacity);
    private int _isCompleted;

    /// <summary>
    ///     关闭当前实例的日志生产端
    /// </summary>
    public void Complete()
    {
        _ = Interlocked.Exchange(ref _isCompleted, 1);
    }

    /// <summary>
    ///     将日志序列化后写入 Redis 队列
    /// </summary>
    /// <param name="entry">日志内容</param>
    /// <returns>日志成功进入队列时返回 true</returns>
    public async Task<bool> EnqueueAsync(ElasticsearchLogEntry entry)
    {
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return false;
        }

        var payload = JsonSerializer.Serialize(entry);
        _ = await _database.ScriptEvaluateAsync(
            EnqueueScript
            , new[] { _key }
            , new RedisValue[] { payload, _capacity }
        ).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     从 Redis 队列原子取出一条日志
    /// </summary>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>取出的日志，没有日志时返回 null</returns>
    public async Task<ElasticsearchLogEntry?> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = await _database.ListLeftPopAsync(_key).ConfigureAwait(false);
            if (!payload.IsNullOrEmpty)
            {
                return JsonSerializer.Deserialize<ElasticsearchLogEntry>(payload.ToString());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     无等待地从 Redis 队列取出一条日志
    /// </summary>
    /// <returns>取出的日志，队列为空时返回 null</returns>
    public async Task<ElasticsearchLogEntry?> TryDequeueAsync()
    {
        var payload = await _database.ListLeftPopAsync(_key).ConfigureAwait(false);
        return payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ElasticsearchLogEntry>(payload.ToString());
    }
}