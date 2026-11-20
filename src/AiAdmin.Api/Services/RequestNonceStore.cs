using AiAdmin.Api.Caching;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     请求随机数的 Redis 存储，用 SET NX 保证同一随机数在有效期内只能使用一次，从而杜绝重放
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接</param>
public sealed class RequestNonceStore(IConnectionMultiplexer connectionMultiplexer)
{
    private const string _KEY_PREFIX = RedisKeyPrefix.VALUE + "request-nonce:";

    /// <summary>
    ///     尝试占用随机数
    /// </summary>
    /// <param name="scope">随机数作用域，通常为用户标识</param>
    /// <param name="nonce">一次性随机数</param>
    /// <param name="ttl">占用有效期，必须不短于时间戳允许窗口的两倍</param>
    /// <returns>首次使用时返回 true，已被使用过时返回 false</returns>
    public Task<bool> TryClaimAsync(
        string scope
        , string nonce
        , TimeSpan ttl
    ) {
        return connectionMultiplexer.GetDatabase().StringSetAsync(_KEY_PREFIX + scope + ":" + nonce, "1", ttl, When.NotExists);
    }
}