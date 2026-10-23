using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     安全停机请求
/// </summary>
public sealed class SafeShutdownRequest
{
    /// <summary>
    ///     在途工作需连续保持为零的稳定时长（秒），避免恰好落在两项工作之间的瞬时空窗被误判为空闲
    /// </summary>
    [Range(1, 60)]
    public int StableSeconds { get; init; } = 2;

    /// <summary>
    ///     最长等待时间（秒），超时仍未空闲则返回未归零的在途工作快照
    /// </summary>
    [Range(1, 3600)]
    public int TimeoutSeconds { get; init; } = 30;
}