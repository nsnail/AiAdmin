using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     缓存中的单条限流规则，只保留请求判定需要的字段
/// </summary>
/// <param name="Id">规则主键</param>
/// <param name="Algorithm">限流算法</param>
/// <param name="Scope">限流范围</param>
/// <param name="RoleCode">指定的角色编码，仅角色范围有效</param>
/// <param name="UserId">指定的用户主键，为空时对范围内每个用户分别生效</param>
/// <param name="Limit">窗口时间内允许的请求数</param>
/// <param name="WindowSeconds">统计窗口秒数</param>
public sealed record RateLimitRuleSnapshot(
    long Id
    , RateLimitAlgorithm Algorithm
    , RateLimitScope Scope
    , string? RoleCode
    , long? UserId
    , int Limit
    , int WindowSeconds);