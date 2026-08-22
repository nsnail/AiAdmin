namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录日志列表响应
/// </summary>
/// <param name="Id">登录日志主键</param>
/// <param name="UserId">登录用户主键</param>
/// <param name="UserName">登录用户名</param>
/// <param name="OwnerId">所有者用户主键</param>
/// <param name="OwnerDepartmentId">所有者部门主键</param>
/// <param name="ClientIp">客户端 IP 地址</param>
/// <param name="Region">IP 归属地区</param>
/// <param name="UserAgent">浏览器原始 User-Agent</param>
/// <param name="OperatingSystem">操作系统名称及版本</param>
/// <param name="Browser">浏览器名称及版本</param>
/// <param name="DeviceType">设备类型</param>
/// <param name="Platform">设备平台</param>
/// <param name="Language">客户端语言</param>
/// <param name="TimeZone">客户端时区</param>
/// <param name="ScreenResolution">屏幕分辨率</param>
/// <param name="ViewportSize">浏览器视口尺寸</param>
/// <param name="ColorDepth">屏幕色深</param>
/// <param name="PixelRatio">设备像素比</param>
/// <param name="TouchPoints">最大触摸点数</param>
/// <param name="ClientHints">浏览器客户端提示原始 JSON</param>
/// <param name="CreatedAt">日志创建时间</param>
public sealed record LoginLogResult(
    long Id
    , long UserId
    , string UserName
    , long OwnerId
    , long OwnerDepartmentId
    , string ClientIp
    , string Region
    , string UserAgent
    , string OperatingSystem
    , string Browser
    , string DeviceType
    , string Platform
    , string Language
    , string TimeZone
    , string ScreenResolution
    , string ViewportSize
    , int? ColorDepth
    , double? PixelRatio
    , int? TouchPoints
    , string ClientHints
    , DateTimeOffset CreatedAt);