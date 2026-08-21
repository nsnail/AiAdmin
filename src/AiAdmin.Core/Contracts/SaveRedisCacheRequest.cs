using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 字符串缓存保存请求
/// </summary>
public sealed class SaveRedisCacheRequest
{
    /// <summary>
    ///     过期时间秒数，零表示永不过期
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ExpireSeconds { get; init; }

    /// <summary>
    ///     缓存键
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    ///     缓存值
    /// </summary>
    public string Value { get; init; } = string.Empty;
}