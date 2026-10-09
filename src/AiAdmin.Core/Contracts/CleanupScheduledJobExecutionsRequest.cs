using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理计划作业执行记录请求
/// </summary>
public sealed class CleanupScheduledJobExecutionsRequest
{
    /// <summary>
    ///     执行记录保留小时数
    /// </summary>
    [Range(1, 87600)]
    public int Hours { get; init; } = 72;
}