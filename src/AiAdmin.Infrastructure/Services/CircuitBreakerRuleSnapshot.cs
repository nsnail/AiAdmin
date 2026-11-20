namespace AiAdmin.Api.Services;

/// <summary>
///     缓存中的单条熔断规则，只保留请求判定需要的字段
/// </summary>
/// <param name="Id">规则主键</param>
/// <param name="FailureRatePercent">触发熔断的失败率百分比</param>
/// <param name="MinimumRequests">评估失败率所需的最小请求数</param>
/// <param name="WindowSeconds">失败率统计窗口秒数</param>
/// <param name="OpenSeconds">打开状态持续的秒数</param>
/// <param name="HalfOpenRequests">半开状态下放行的试探请求数</param>
/// <param name="SlowCallMs">慢调用阈值毫秒数，0 表示不统计慢调用</param>
public sealed record CircuitBreakerRuleSnapshot(
    long Id
    , int FailureRatePercent
    , int MinimumRequests
    , int WindowSeconds
    , int OpenSeconds
    , int HalfOpenRequests
    , int SlowCallMs);