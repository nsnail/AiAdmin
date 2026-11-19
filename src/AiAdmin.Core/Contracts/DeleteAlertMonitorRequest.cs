using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     删除告警监控请求，单条和批量删除共用
/// </summary>
public sealed class DeleteAlertMonitorRequest
{
    /// <summary>
    ///     待删除的监控主键集合
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<long> Ids { get; init; } = [];
}