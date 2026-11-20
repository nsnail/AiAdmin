namespace AiAdmin.Api.Models;

/// <summary>
///     熔断器状态
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    ///     关闭，请求正常放行并统计失败率
    /// </summary>
    Closed = 1

    ,

    /// <summary>
    ///     打开，直接拒绝请求，持续熔断时长后进入半开
    /// </summary>
    Open = 2

    ,

    /// <summary>
    ///     半开，只放行少量试探请求，成功则恢复关闭，失败则重新打开
    /// </summary>
    HalfOpen = 3
}