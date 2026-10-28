namespace AiAdmin.Api.Contracts;

/// <summary>
///     API 请求日志列表项，整数形式的路径、IP 和方法已还原为可读值
/// </summary>
/// <param name="Id">日志主键，字符串形式避免雪花编号精度丢失</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="Method">HTTP 方法名称</param>
/// <param name="Path">请求路径，接口表中找不到对应接口时为空</param>
/// <param name="ApiName">接口显示名称，接口表中找不到对应接口时为空</param>
/// <param name="PathCrc32">请求路径的 CRC32</param>
/// <param name="StatusCode">HTTP 状态码</param>
/// <param name="ApiResponseCode">API 响应业务编码</param>
/// <param name="ElapsedMilliseconds">请求耗时毫秒数</param>
/// <param name="UserId">用户主键，字符串形式，未登录时为空</param>
/// <param name="UserName">用户名，用户已不存在或未登录时为空</param>
/// <param name="DepartmentId">所属部门主键，字符串形式</param>
/// <param name="ClientIp">客户端 IPv4 地址</param>
/// <param name="ServerIp">服务端 IPv4 地址</param>
/// <param name="TraceId">链路主键，字符串形式</param>
/// <param name="WorkerId">处理请求的实例编号</param>
public sealed record ApiRequestLogResult(
    string Id
    , DateTimeOffset CreatedAt
    , string Method
    , string? Path
    , string? ApiName
    , int PathCrc32
    , int StatusCode
    , int? ApiResponseCode
    , int ElapsedMilliseconds
    , string? UserId
    , string? UserName
    , string? DepartmentId
    , string? ClientIp
    , string? ServerIp
    , string TraceId
    , int WorkerId);