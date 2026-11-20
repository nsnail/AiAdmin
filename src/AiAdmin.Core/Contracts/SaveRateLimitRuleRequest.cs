using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     限流规则保存请求，新增和修改共用
/// </summary>
public sealed class SaveRateLimitRuleRequest
{
    /// <summary>
    ///     限流算法，默认滑动窗口
    /// </summary>
    public RateLimitAlgorithm Algorithm { get; init; } = RateLimitAlgorithm.SlidingWindow;

    /// <summary>
    ///     关联接口主键
    /// </summary>
    [Range(1, long.MaxValue)]
    public long ApiEndpointId { get; init; }

    /// <summary>
    ///     规则主键，修改时必填
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    ///     窗口时间内允许的请求数，令牌桶和漏桶中同时是桶容量
    /// </summary>
    [Range(1, 1_000_000)]
    public int Limit { get; init; }

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    public string Remark { get; init; } = string.Empty;

    /// <summary>
    ///     指定的角色编码，仅角色范围有效且必填
    /// </summary>
    [MaxLength(50)]
    public string? RoleCode { get; init; }

    /// <summary>
    ///     限流范围，默认全局
    /// </summary>
    public RateLimitScope Scope { get; init; } = RateLimitScope.Global;

    /// <summary>
    ///     指定的用户主键，仅用户范围有效，为空时对每个用户分别生效
    /// </summary>
    public long? UserId { get; init; }

    /// <summary>
    ///     统计窗口秒数
    /// </summary>
    [Range(1, 86_400)]
    public int WindowSeconds { get; init; }
}