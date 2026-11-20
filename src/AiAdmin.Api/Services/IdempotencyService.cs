using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Caching;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     接口幂等的 Redis 存储：用 SET NX 原子占位，完成后写入可回放的响应，失败时释放占位以便重试
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接</param>
public sealed class IdempotencyService(IConnectionMultiplexer connectionMultiplexer)
{
    /// <summary>
    ///     Redis 键前缀
    /// </summary>
    public const string KEY_PREFIX = RedisKeyPrefix.VALUE + "idempotency:";

    /// <summary>
    ///     计算请求内容的 SHA256 指纹
    /// </summary>
    /// <param name="content">请求内容文本</param>
    /// <returns>十六进制指纹</returns>
    public static string ComputeFingerprint(string content) {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    /// <summary>
    ///     释放占位，让客户端可以用同一个键重试
    /// </summary>
    /// <param name="key">幂等键</param>
    /// <returns>异步释放任务</returns>
    public Task AbandonAsync(string key) {
        return connectionMultiplexer.GetDatabase().KeyDeleteAsync(key);
    }

    /// <summary>
    ///     尝试占位：首次请求返回 Started，其余情况按已有记录区分处理中、回放和内容不一致
    /// </summary>
    /// <param name="key">幂等键</param>
    /// <param name="fingerprint">请求内容指纹</param>
    /// <param name="lockTtl">占位有效期，进程异常退出时用于自动释放</param>
    /// <returns>检查结果</returns>
    public async Task<IdempotencyBeginResult> BeginAsync(
        string key
        , string fingerprint
        , TimeSpan lockTtl
    ) {
        var database = connectionMultiplexer.GetDatabase();
        var placeholder = JsonSerializer.Serialize(new IdempotencyEntry(fingerprint, false, 0, null));
        for (var attempt = 0; attempt < 3; ++attempt) {
            if (await database.StringSetAsync(key, placeholder, lockTtl, When.NotExists).ConfigureAwait(false)) {
                return new IdempotencyBeginResult(IdempotencyState.Started, null);
            }

            var stored = await database.StringGetAsync(key).ConfigureAwait(false);
            if (!stored.HasValue) {
                // 记录在两次调用之间过期，重新占位
                continue;
            }

            var entry = JsonSerializer.Deserialize<IdempotencyEntry>(stored.ToString());
            if (entry is null) {
                continue;
            }

            if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal)) {
                return new IdempotencyBeginResult(IdempotencyState.Mismatch, null);
            }

            return entry.Completed
                ? new IdempotencyBeginResult(IdempotencyState.Replay, entry)
                : new IdempotencyBeginResult(IdempotencyState.InProgress, null);
        }

        return new IdempotencyBeginResult(IdempotencyState.InProgress, null);
    }

    /// <summary>
    ///     保存成功响应，覆盖占位记录
    /// </summary>
    /// <param name="key">幂等键</param>
    /// <param name="fingerprint">请求内容指纹</param>
    /// <param name="statusCode">响应状态码</param>
    /// <param name="body">响应 JSON 文本</param>
    /// <param name="ttl">记录保留时长</param>
    /// <returns>异步保存任务</returns>
    public Task CompleteAsync(
        string key
        , string fingerprint
        , int statusCode
        , string body
        , TimeSpan ttl
    ) {
        var value = JsonSerializer.Serialize(new IdempotencyEntry(fingerprint, true, statusCode, body));
        return connectionMultiplexer.GetDatabase().StringSetAsync(key, value, ttl);
    }
}