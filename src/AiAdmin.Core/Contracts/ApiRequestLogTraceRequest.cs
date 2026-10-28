using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     按链路编号查询 Elasticsearch 日志的请求
/// </summary>
public sealed class ApiRequestLogTraceRequest
{
    /// <summary>
    ///     链路编号，即请求日志中的 TraceId，雪花编号，字符串形式避免前端精度丢失
    /// </summary>
    [Required]
    [RegularExpression("^[0-9]{1,19}$", ErrorMessage = "TraceId must be a positive integer")]
    public string TraceId { get; init; } = string.Empty;
}