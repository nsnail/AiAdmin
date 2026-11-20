using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     删除熔断规则请求，单条和批量删除共用
/// </summary>
public sealed class DeleteCircuitBreakerRuleRequest
{
    /// <summary>
    ///     待删除的规则主键集合
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<long> Ids { get; init; } = [];
}