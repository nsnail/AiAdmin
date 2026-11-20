using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     熔断器对一次请求的放行判定
/// </summary>
/// <param name="Allowed">请求是否放行</param>
/// <param name="State">判定时熔断器的状态</param>
/// <param name="RetryAfterSeconds">被拒绝时建议等待的秒数，放行时为 0</param>
/// <param name="Probe">放行的是否是半开状态下的试探请求，试探请求的结果决定熔断器恢复还是重新打开</param>
public sealed record CircuitBreakerPermit(bool Allowed, CircuitBreakerState State, int RetryAfterSeconds, bool Probe);