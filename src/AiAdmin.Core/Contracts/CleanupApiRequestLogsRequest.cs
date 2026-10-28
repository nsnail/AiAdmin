using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理 API 请求日志分表请求
/// </summary>
public sealed class CleanupApiRequestLogsRequest
{
    /// <summary>
    ///     保留天数，早于该天数之前的日分表整张删除，含当天共保留该天数的日志
    /// </summary>
    [Range(1, 3650)]
    public int Days { get; init; } = 30;
}