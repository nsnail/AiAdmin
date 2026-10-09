using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     批量更新计划作业备注请求
/// </summary>
public sealed class BatchUpdateScheduledJobRemarkRequest
{
    /// <summary>
    ///     计划作业编号集合
    /// </summary>
    [MinLength(1)]
    public required long[] Ids { get; init; }

    /// <summary>
    ///     备注
    /// </summary>
    [MaxLength(500)]
    public string Remark { get; init; } = string.Empty;
}