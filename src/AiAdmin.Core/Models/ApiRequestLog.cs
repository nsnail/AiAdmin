using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     API 请求日志实体，只记录元数据，按天分表保存，表名为 sys_api_request_log_yyyyMMdd，不参与 EF 主模型；
///     单表可达千万级，所有列均为定长数值，不保存任何变长字符串，路径、IP 等以数字形式存储
/// </summary>
public sealed class ApiRequestLog
{
    /// <summary>
    ///     API 响应业务编码，响应体不是标准结构或超出范围时为空
    /// </summary>
    [ListFilter("apiRequestLog.fields.apiResponseCode", Sort = 6, IsVisible = false)]
    public short? ApiResponseCode { get; init; }

    /// <summary>
    ///     客户端 IPv4 地址的 32 位整数，非 IPv4 或无法获取时为空
    /// </summary>
    [ListFilter("apiRequestLog.fields.clientIp", Placeholder = "apiRequestLog.placeholder.clientIp", Span = 3, Sort = 4)]
    public int? ClientIp { get; init; }

    /// <summary>
    ///     创建时间，UTC，决定所属的日分表
    /// </summary>
    [ListFilter("listFilter.common.createdAt", "date", Span = 7, Sort = int.MinValue)]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     所属部门主键
    /// </summary>
    public long? DepartmentId { get; init; }

    /// <summary>
    ///     请求耗时毫秒数，超过 int 上限时取上限
    /// </summary>
    [ListFilter("apiRequestLog.fields.elapsedMilliseconds", "number", Span = 3, Sort = 7, IsVisible = false)]
    public int ElapsedMilliseconds { get; init; }

    /// <summary>
    ///     主键，雪花编号
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     HTTP 方法编号
    /// </summary>
    [ListFilter(
        "apiRequestLog.fields.method", "select"
        , Options = ["1:GET", "2:POST", "3:PUT", "4:DELETE", "5:PATCH", "6:HEAD", "7:OPTIONS", "0:apiRequestLog.method.other"], GroupCount = true
        , Span = 2, Sort = 3
    )]
    public ApiRequestMethod Method { get; init; }

    /// <summary>
    ///     请求路径（不含查询字符串，规范化为小写）的 CRC32，查询时与接口表的路径 CRC32 对应
    /// </summary>
    [ListFilter("apiRequestLog.fields.api", "api-select", Placeholder = "apiRequestLog.placeholder.api", GroupCount = true, Span = 4, Sort = 2)]
    public int PathCrc32 { get; init; }

    /// <summary>
    ///     服务端 IPv4 地址的 32 位整数，非 IPv4 或无法获取时为空
    /// </summary>
    public int? ServerIp { get; init; }

    /// <summary>
    ///     HTTP 状态码
    /// </summary>
    [ListFilter(
        "apiRequestLog.fields.statusCode", "select"
        , Options =
        [
            "200:200", "201:201", "204:204", "301:301", "302:302", "304:304", "400:400", "401:401", "403:403", "404:404", "405:405", "409:409"
            , "415:415", "429:429", "500:500", "502:502", "503:503", "504:504"
        ], GroupCount = true, Span = 2, Sort = 5
    )]
    public short StatusCode { get; init; }

    /// <summary>
    ///     链路主键，与日志中的 TraceId 一致
    /// </summary>
    [ListFilter("apiRequestLog.fields.traceId", Placeholder = "apiRequestLog.placeholder.traceId", Span = 4, Sort = 8, IsVisible = false)]
    public long TraceId { get; init; }

    /// <summary>
    ///     用户主键，未登录时为空
    /// </summary>
    [ListFilter("apiRequestLog.fields.user", "user-select", Span = 4, Sort = 1)]
    public long? UserId { get; init; }

    /// <summary>
    ///     处理请求的实例编号
    /// </summary>
    public short WorkerId { get; init; }
}