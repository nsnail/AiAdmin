namespace AiAdmin.Api.Services;

/// <summary>
///     限流判定结果
/// </summary>
/// <param name="Allowed">请求是否放行</param>
/// <param name="RetryAfterSeconds">被拒绝时建议等待的秒数，放行时为 0</param>
/// <param name="Rule">被拒绝时触发的限流规则，放行时为空值</param>
public sealed record RateLimitDecision(bool Allowed, int RetryAfterSeconds, RateLimitRuleSnapshot? Rule = null);