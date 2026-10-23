using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理计划作业执行记录请求
/// </summary>
public sealed class CleanupExecutionsRequest
{
    /// <summary>
    ///     保留天数，开始时间早于该天数之前的执行记录会被删除
    /// </summary>
    [Range(1, 3650)]
    public int Days { get; init; } = 30;
}