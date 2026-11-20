using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     熔断规则保存请求，新增和修改共用
/// </summary>
public sealed class SaveCircuitBreakerRuleRequest
{
    /// <summary>
    ///     关联接口主键
    /// </summary>
    [Range(1, long.MaxValue)]
    public long ApiEndpointId { get; init; }

    /// <summary>
    ///     触发熔断的失败率百分比
    /// </summary>
    [Range(1, 100)]
    public int FailureRatePercent { get; init; } = 50;

    /// <summary>
    ///     半开状态下放行的试探请求数
    /// </summary>
    [Range(1, 100)]
    public int HalfOpenRequests { get; init; } = 3;

    /// <summary>
    ///     规则主键，修改时必填
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     评估失败率所需的最小请求数
    /// </summary>
    [Range(1, 1_000_000)]
    public int MinimumRequests { get; init; } = 20;

    /// <summary>
    ///     打开状态持续的秒数
    /// </summary>
    [Range(1, 3600)]
    public int OpenSeconds { get; init; } = 30;

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    public string Remark { get; init; } = string.Empty;

    /// <summary>
    ///     慢调用阈值毫秒数，0 表示不统计慢调用
    /// </summary>
    [Range(0, 600_000)]
    public int SlowCallMs { get; init; }

    /// <summary>
    ///     失败率统计窗口秒数
    /// </summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}