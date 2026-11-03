namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘最近登录记录
/// </summary>
/// <param name="Id">登录日志主键，用于前端按主键查询完整记录</param>
/// <param name="UserName">用户名</param>
/// <param name="ClientIp">客户端地址</param>
/// <param name="Region">登录地区</param>
/// <param name="Browser">浏览器</param>
/// <param name="CreatedAt">登录时间</param>
public sealed record DashboardRecentLoginResult(long Id, string UserName, string ClientIp, string Region, string Browser, DateTimeOffset CreatedAt);