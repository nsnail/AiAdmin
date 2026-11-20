using System.Globalization;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Models;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     基于 Redis Lua 脚本的限流器，滑动窗口、漏桶和令牌桶都在脚本内原子完成，时间取 Redis 服务器时间，多实例之间计数一致
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
public sealed class RateLimiter(IConnectionMultiplexer connectionMultiplexer)
{
    /// <summary>
    ///     漏桶脚本：请求入桶使水位加一，水位按恒定速率下降，加入后超过容量时拒绝，输出速率恒定
    /// </summary>
    private const string _LEAKY_BUCKET_SCRIPT = """
                                                local t = redis.call('TIME')
                                                local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
                                                local window = tonumber(ARGV[1])
                                                local capacity = tonumber(ARGV[2])
                                                local rate = capacity / window
                                                local state = redis.call('HMGET', KEYS[1], 'v', 'ts')
                                                local level = tonumber(state[1])
                                                local ts = tonumber(state[2])
                                                if level == nil then
                                                    level = 0
                                                    ts = now
                                                end
                                                level = math.max(0, level - math.max(0, now - ts) * rate)
                                                local allowed = 0
                                                local retry = 0
                                                if level + 1 <= capacity then
                                                    level = level + 1
                                                    allowed = 1
                                                else
                                                    retry = math.ceil((level + 1 - capacity) / rate)
                                                end
                                                redis.call('HSET', KEYS[1], 'v', string.format('%.6f', level), 'ts', now)
                                                redis.call('PEXPIRE', KEYS[1], window * 2)
                                                return {allowed, retry}
                                                """;

    /// <summary>
    ///     滑动窗口脚本：有序集合记录每次请求的时间戳，窗口内的请求数达到上限时拒绝
    /// </summary>
    private const string _SLIDING_WINDOW_SCRIPT = """
                                                  local t = redis.call('TIME')
                                                  local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
                                                  local window = tonumber(ARGV[1])
                                                  local limit = tonumber(ARGV[2])
                                                  redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now - window)
                                                  local count = redis.call('ZCARD', KEYS[1])
                                                  if count < limit then
                                                      redis.call('ZADD', KEYS[1], now, now .. ':' .. ARGV[3])
                                                      redis.call('PEXPIRE', KEYS[1], window)
                                                      return {1, 0}
                                                  end
                                                  local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
                                                  return {0, math.max(1, tonumber(oldest[2]) + window - now)}
                                                  """;

    /// <summary>
    ///     令牌桶脚本：按恒定速率补充令牌，容量为上限，每个请求消耗一个令牌，允许桶容量内的突发
    /// </summary>
    private const string _TOKEN_BUCKET_SCRIPT = """
                                                local t = redis.call('TIME')
                                                local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
                                                local window = tonumber(ARGV[1])
                                                local capacity = tonumber(ARGV[2])
                                                local rate = capacity / window
                                                local state = redis.call('HMGET', KEYS[1], 'v', 'ts')
                                                local tokens = tonumber(state[1])
                                                local ts = tonumber(state[2])
                                                if tokens == nil then
                                                    tokens = capacity
                                                    ts = now
                                                end
                                                tokens = math.min(capacity, tokens + math.max(0, now - ts) * rate)
                                                local allowed = 0
                                                local retry = 0
                                                if tokens >= 1 then
                                                    tokens = tokens - 1
                                                    allowed = 1
                                                else
                                                    retry = math.ceil((1 - tokens) / rate)
                                                end
                                                redis.call('HSET', KEYS[1], 'v', string.format('%.6f', tokens), 'ts', now)
                                                redis.call('PEXPIRE', KEYS[1], window * 2)
                                                return {allowed, retry}
                                                """;

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    /// <summary>
    ///     按规则对一个限流主体做一次判定，放行时已计入该次请求
    /// </summary>
    /// <param name="rule">限流规则</param>
    /// <param name="subject">限流主体标识，全局规则为固定值，用户规则为用户主键或客户端地址</param>
    /// <returns>判定结果，被拒绝时包含建议等待秒数</returns>
    public async Task<RateLimitDecision> CheckAsync(
        RateLimitRuleSnapshot rule
        , string subject
    ) {
        // 键里带上算法编号，规则改了算法后不会和旧数据结构类型冲突
        RedisKey key = $"{RedisKeyPrefix.VALUE}rate-limit:{rule.Id.ToString(CultureInfo.InvariantCulture)}:{(int)rule.Algorithm}:{subject}";
        var windowMs = (long)rule.WindowSeconds * 1000;
        var script = rule.Algorithm switch
        {
            RateLimitAlgorithm.LeakyBucket => _LEAKY_BUCKET_SCRIPT
            , RateLimitAlgorithm.TokenBucket => _TOKEN_BUCKET_SCRIPT
            , _ => _SLIDING_WINDOW_SCRIPT
        };
        RedisValue[] values = [windowMs, rule.Limit, Guid.NewGuid().ToString("N")];
        var result = (long[]?)await _database.ScriptEvaluateAsync(script, [key], values).ConfigureAwait(false);
        return result is [1, _]
            ? new RateLimitDecision(true, 0)
            : new RateLimitDecision(false, (int)Math.Max(1, (long)Math.Ceiling((result?[1] ?? 1000) / 1000d)), rule);
    }
}