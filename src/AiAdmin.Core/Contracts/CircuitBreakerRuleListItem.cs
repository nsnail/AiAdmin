using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     熔断规则列表项，包含熔断器当前状态
/// </summary>
/// <param name="Id">规则主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="ApiEndpointId">接口主键</param>
/// <param name="Method">接口请求方法</param>
/// <param name="Path">接口路径</param>
/// <param name="ApiName">接口名称</param>
/// <param name="FailureRatePercent">触发熔断的失败率百分比</param>
/// <param name="MinimumRequests">评估失败率所需的最小请求数</param>
/// <param name="WindowSeconds">失败率统计窗口秒数</param>
/// <param name="OpenSeconds">打开状态持续的秒数</param>
/// <param name="HalfOpenRequests">半开状态下放行的试探请求数</param>
/// <param name="SlowCallMs">慢调用阈值毫秒数</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="Remark">备注</param>
/// <param name="UpdatedAt">最后更新时间</param>
/// <param name="State">熔断器当前状态，未启用时为关闭</param>
/// <param name="RetryAfterSeconds">打开状态下距离进入半开的剩余秒数，其余状态为 0</param>
public sealed record CircuitBreakerRuleListItem(
    long Id
    , DateTimeOffset CreatedAt
    , long ApiEndpointId
    , string Method
    , string Path
    , string ApiName
    , int FailureRatePercent
    , int MinimumRequests
    , int WindowSeconds
    , int OpenSeconds
    , int HalfOpenRequests
    , int SlowCallMs
    , bool IsEnabled
    , string Remark
    , DateTimeOffset? UpdatedAt
    , CircuitBreakerState State
    , int RetryAfterSeconds);