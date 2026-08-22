namespace AiAdmin.Api.Contracts;

/// <summary>
///     上次登录摘要
/// </summary>
/// <param name="ClientIp">客户端 IP 地址</param>
/// <param name="Region">IP 归属地区</param>
/// <param name="LoginAt">登录时间</param>
public sealed record PreviousLoginResult(string ClientIp, string Region, DateTimeOffset LoginAt);