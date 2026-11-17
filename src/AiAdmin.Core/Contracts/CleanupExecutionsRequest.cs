using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理计划作业执行记录请求
/// </summary>
public sealed class CleanupExecutionsRequest
{
    /// <summary>
    ///     保留小时数，开始时间早于该小时数之前的执行记录会被删除
    /// </summary>
    [Range(1, 87600)]
    public int Hours { get; init; } = 720;
}