using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     请求被限流时返回给客户端的触发规则信息
/// </summary>
/// <param name="RuleId">触发的规则主键</param>
/// <param name="Algorithm">限流算法</param>
/// <param name="Scope">限流范围</param>
/// <param name="RoleCode">角色范围时的角色编码</param>
/// <param name="Limit">窗口时间内允许的请求数</param>
/// <param name="WindowSeconds">统计窗口秒数</param>
/// <param name="RetryAfterSeconds">建议等待的秒数</param>
public sealed record RateLimitRejection(
    string RuleId
    , RateLimitAlgorithm Algorithm
    , RateLimitScope Scope
    , string? RoleCode
    , int Limit
    , int WindowSeconds
    , int RetryAfterSeconds);