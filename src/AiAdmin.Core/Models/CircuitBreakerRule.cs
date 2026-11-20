using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     接口熔断规则实体，每个接口最多一条；窗口内失败率达到阈值时暂停放行该接口，到期后以少量试探请求探测恢复
/// </summary>
public sealed class CircuitBreakerRule : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     关联接口
    /// </summary>
    public ApiEndpoint ApiEndpoint { get; init; } = null!;

    /// <summary>
    ///     关联接口主键
    /// </summary>
    public long ApiEndpointId { get; set; }

    /// <summary>
    ///     触发熔断的失败率百分比，窗口内失败请求占比达到该值时熔断
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.failureRatePercent", "number", Span = 2, Sort = 1, IsVisible = false)]
    public int FailureRatePercent { get; set; } = 50;

    /// <summary>
    ///     半开状态下放行的试探请求数，全部成功才恢复关闭
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.halfOpenRequests", "number", Span = 2, Sort = 4, IsVisible = false)]
    public int HalfOpenRequests { get; set; } = 3;

    /// <summary>
    ///     规则主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     是否启用
    /// </summary>
    [ListFilter(
        "listFilter.common.status", "select", Options = ["true:listFilter.option.enabled", "false:listFilter.option.disabled"], GroupCount = true
        , Span = 2, Sort = 5
    )]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     窗口内至少达到该请求数才评估失败率，避免低流量时误熔断
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.minimumRequests", "number", Span = 2, Sort = 2, IsVisible = false)]
    public int MinimumRequests { get; set; } = 20;

    /// <summary>
    ///     打开状态持续的秒数，到期后进入半开
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.openSeconds", "number", Span = 2, Sort = 3, IsVisible = false)]
    public int OpenSeconds { get; set; } = 30;

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    [ListFilter("listFilter.circuitBreaker.remark", Placeholder = "listFilter.circuitBreaker.remarkPlaceholder", Span = 3, Sort = 6)]
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    ///     慢调用阈值毫秒数，耗时超过该值的请求也按失败统计，0 表示不统计慢调用
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.slowCallMs", "number", Span = 2, Sort = 5, IsVisible = false)]
    public int SlowCallMs { get; set; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     失败率的统计窗口秒数
    /// </summary>
    [ListFilter("listFilter.circuitBreaker.windowSeconds", "number", Span = 2, Sort = 7, IsVisible = false)]
    public int WindowSeconds { get; set; } = 60;
}