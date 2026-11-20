using System.Globalization;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Models;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     基于 Redis Lua 脚本的三态熔断器，状态和统计存在 Redis，所有实例共享同一份状态，时间取 Redis 服务器时间；
///     请求前调用 <see cref="TryAcquireAsync" /> 判定是否放行，请求结束后调用 <see cref="ReportAsync" /> 上报结果
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
public sealed class CircuitBreaker(IConnectionMultiplexer connectionMultiplexer)
{
    /// <summary>
    ///     放行判定脚本：关闭状态直接放行；打开状态到期后转半开并放行试探请求，未到期拒绝；半开状态只放行配额内的试探请求。
    ///     返回 {放行(1/0), 状态(1关闭 2打开 3半开), 剩余毫秒, 是否试探(1/0)}
    /// </summary>
    private const string _ACQUIRE_SCRIPT = """
                                           local t = redis.call('TIME')
                                           local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
                                           local openMs = tonumber(ARGV[1]) * 1000
                                           local quota = tonumber(ARGV[2])
                                           local state = tonumber(redis.call('HGET', KEYS[1], 'state') or '1')
                                           if state == 1 then
                                               return {1, 1, 0, 0}
                                           end
                                           if state == 2 then
                                               local openedAt = tonumber(redis.call('HGET', KEYS[1], 'openedAt') or '0')
                                               local remain = openedAt + openMs - now
                                               if remain > 0 then
                                                   return {0, 2, remain, 0}
                                               end
                                               redis.call('HSET', KEYS[1], 'state', 3, 'probes', 1, 'success', 0)
                                               return {1, 3, 0, 1}
                                           end
                                           local probes = tonumber(redis.call('HGET', KEYS[1], 'probes') or '0')
                                           if probes < quota then
                                               redis.call('HINCRBY', KEYS[1], 'probes', 1)
                                               return {1, 3, 0, 1}
                                           end
                                           return {0, 3, 1000, 0}
                                           """;

    /// <summary>
    ///     结果上报脚本：关闭状态下按窗口统计，请求数达到最小样本且失败率达到阈值时打开；
    ///     半开状态下试探请求失败立即重新打开，全部试探成功则恢复关闭并清空统计。
    ///     返回 {状态(1关闭 2打开 3半开)}
    /// </summary>
    private const string _REPORT_SCRIPT = """
                                          local t = redis.call('TIME')
                                          local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
                                          local failed = tonumber(ARGV[1])
                                          local probe = tonumber(ARGV[2])
                                          local windowMs = tonumber(ARGV[3]) * 1000
                                          local minRequests = tonumber(ARGV[4])
                                          local ratePercent = tonumber(ARGV[5])
                                          local quota = tonumber(ARGV[6])
                                          local state = tonumber(redis.call('HGET', KEYS[1], 'state') or '1')
                                          if probe == 1 then
                                              if state ~= 3 then
                                                  return {state}
                                              end
                                              if failed == 1 then
                                                  redis.call('HSET', KEYS[1], 'state', 2, 'openedAt', now)
                                                  return {2}
                                              end
                                              local success = redis.call('HINCRBY', KEYS[1], 'success', 1)
                                              if success >= quota then
                                                  redis.call('DEL', KEYS[1])
                                                  return {1}
                                              end
                                              return {3}
                                          end
                                          if state ~= 1 then
                                              return {state}
                                          end
                                          local windowStart = tonumber(redis.call('HGET', KEYS[1], 'windowStart') or '0')
                                          if windowStart == 0 or now - windowStart >= windowMs then
                                              redis.call('HSET', KEYS[1], 'windowStart', now, 'total', 0, 'failures', 0)
                                          end
                                          local total = redis.call('HINCRBY', KEYS[1], 'total', 1)
                                          local failures = tonumber(redis.call('HGET', KEYS[1], 'failures') or '0')
                                          if failed == 1 then
                                              failures = redis.call('HINCRBY', KEYS[1], 'failures', 1)
                                          end
                                          redis.call('PEXPIRE', KEYS[1], windowMs * 4)
                                          if total >= minRequests and failures * 100 >= total * ratePercent then
                                              redis.call('HSET', KEYS[1], 'state', 2, 'openedAt', now)
                                              redis.call('PERSIST', KEYS[1])
                                              return {2}
                                          end
                                          return {1}
                                          """;

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    /// <summary>
    ///     读取熔断器当前状态，只读取不推进状态机，供管理页展示
    /// </summary>
    /// <param name="rule">熔断规则</param>
    /// <returns>当前状态和打开状态下的剩余秒数</returns>
    public async Task<(CircuitBreakerState State, int RetryAfterSeconds)> GetStateAsync(CircuitBreakerRuleSnapshot rule) {
        var entries = await _database.HashGetAsync(Key(rule), ["state", "openedAt"]).ConfigureAwait(false);
        var state = entries[0].HasValue ? (CircuitBreakerState)(int)entries[0] : CircuitBreakerState.Closed;
        if (state != CircuitBreakerState.Open || !entries[1].HasValue) {
            return (state, 0);
        }

        // 到期后在下一次请求才会转为半开，这里展示为半开，避免界面上一直显示打开
        var remainMs = (long)entries[1] + ((long)rule.OpenSeconds * 1000) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return remainMs > 0 ? (state, (int)Math.Ceiling(remainMs / 1000d)) : (CircuitBreakerState.HalfOpen, 0);
    }

    /// <summary>
    ///     请求结束后上报结果，更新失败统计或推进状态机
    /// </summary>
    /// <param name="rule">熔断规则</param>
    /// <param name="failed">请求是否算作失败</param>
    /// <param name="probe">是否是半开状态下的试探请求</param>
    /// <returns>上报后熔断器的状态</returns>
    public async Task<CircuitBreakerState> ReportAsync(
        CircuitBreakerRuleSnapshot rule
        , bool failed
        , bool probe
    ) {
        RedisValue[] values =
        [
            failed ? 1 : 0, probe ? 1 : 0, rule.WindowSeconds, rule.MinimumRequests, rule.FailureRatePercent, rule.HalfOpenRequests
        ];
        var result = (long[]?)await _database.ScriptEvaluateAsync(_REPORT_SCRIPT, [Key(rule)], values).ConfigureAwait(false);
        return result is { Length: 1 } ? (CircuitBreakerState)result[0] : CircuitBreakerState.Closed;
    }

    /// <summary>
    ///     请求开始前判定是否放行
    /// </summary>
    /// <param name="rule">熔断规则</param>
    /// <returns>放行判定，被拒绝时包含剩余等待秒数</returns>
    public async Task<CircuitBreakerPermit> TryAcquireAsync(CircuitBreakerRuleSnapshot rule) {
        RedisValue[] values = [rule.OpenSeconds, rule.HalfOpenRequests];
        var result = (long[]?)await _database.ScriptEvaluateAsync(_ACQUIRE_SCRIPT, [Key(rule)], values).ConfigureAwait(false);
        if (result is not { Length: 4 }) {
            return new CircuitBreakerPermit(true, CircuitBreakerState.Closed, 0, false);
        }

        return new CircuitBreakerPermit(
            result[0] == 1, (CircuitBreakerState)result[1], (int)Math.Max(result[0] == 1 ? 0 : 1, (long)Math.Ceiling(result[2] / 1000d))
            , result[3] == 1
        );
    }

    /// <summary>
    ///     生成熔断器状态键，按规则主键区分，规则删除重建后状态不会沿用
    /// </summary>
    /// <param name="rule">熔断规则</param>
    /// <returns>Redis 键</returns>
    private static RedisKey Key(CircuitBreakerRuleSnapshot rule) {
        return $"{RedisKeyPrefix.VALUE}circuit-breaker:{rule.Id.ToString(CultureInfo.InvariantCulture)}";
    }
}