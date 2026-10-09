using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     单条资源标识请求
/// </summary>
public sealed class IdentifierRequest
{
    /// <summary>
    ///     资源主键
    /// </summary>
    [Range(1, long.MaxValue)]
    public long Id { get; init; }
}