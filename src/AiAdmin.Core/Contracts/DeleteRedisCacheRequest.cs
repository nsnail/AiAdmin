using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 缓存键删除请求
/// </summary>
public sealed class DeleteRedisCacheRequest
{
    /// <summary>
    ///     缓存键
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Key { get; init; } = string.Empty;
}