using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     请求被熔断拒绝时返回给客户端的信息
/// </summary>
/// <param name="RuleId">触发的熔断规则主键</param>
/// <param name="State">熔断器当前状态</param>
/// <param name="RetryAfterSeconds">建议等待的秒数</param>
public sealed record CircuitBreakerRejection(string RuleId, CircuitBreakerState State, int RetryAfterSeconds);