using System.ComponentModel.DataAnnotations;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     接口限流规则实体，一个接口可配置多条规则，请求必须同时满足所有已启用的规则
/// </summary>
public sealed class RateLimitRule : EntityBase, IUpdatedAt
{
    /// <summary>
    ///     限流算法
    /// </summary>
    [ListFilter(
        "listFilter.rateLimit.algorithm", "select"
        , Options = ["1:listFilter.rateLimit.slidingWindow", "2:listFilter.rateLimit.leakyBucket", "3:listFilter.rateLimit.tokenBucket"]
        , GroupCount = true, Span = 3, Sort = 1
    )]
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;

    /// <summary>
    ///     关联接口
    /// </summary>
    public ApiEndpoint ApiEndpoint { get; init; } = null!;

    /// <summary>
    ///     关联接口主键
    /// </summary>
    public long ApiEndpointId { get; set; }

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
    ///     窗口时间内允许的请求数，令牌桶和漏桶中同时是桶容量
    /// </summary>
    [ListFilter("listFilter.rateLimit.limit", "number", Span = 2, Sort = 3, IsVisible = false)]
    public int Limit { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    [ListFilter("listFilter.rateLimit.remark", Placeholder = "listFilter.rateLimit.remarkPlaceholder", Span = 3, Sort = 6)]
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    ///     指定的角色编码，仅角色范围有效；拥有该角色的用户适用本规则，每个用户分别计数
    /// </summary>
    [MaxLength(50)]
    [ListFilter("listFilter.rateLimit.roleCode", Placeholder = "listFilter.rateLimit.roleCodePlaceholder", Span = 3, Sort = 4)]
    public string? RoleCode { get; set; }

    /// <summary>
    ///     限流范围
    /// </summary>
    [ListFilter(
        "listFilter.rateLimit.scope", "select"
        , Options = ["1:listFilter.rateLimit.global", "2:listFilter.rateLimit.user", "3:listFilter.rateLimit.role"], GroupCount = true, Span = 2
        , Sort = 2
    )]
    public RateLimitScope Scope { get; set; } = RateLimitScope.Global;

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     指定的用户主键，仅用户范围有效；设置后规则只对该用户生效，为空时对每个用户分别生效
    /// </summary>
    [ListFilter("listFilter.rateLimit.userId", "number", Span = 3, Sort = 5, IsVisible = false)]
    public long? UserId { get; set; }

    /// <summary>
    ///     统计窗口秒数，令牌桶和漏桶中按 Limit 除以该值得到补充或流出速率
    /// </summary>
    [ListFilter("listFilter.rateLimit.windowSeconds", "number", Span = 2, Sort = 7, IsVisible = false)]
    public int WindowSeconds { get; set; }
}