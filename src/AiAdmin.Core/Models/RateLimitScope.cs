namespace AiAdmin.Api.Models;

/// <summary>
///     限流范围
/// </summary>
public enum RateLimitScope
{
    /// <summary>
    ///     全局，所有请求共用同一个计数
    /// </summary>
    Global = 1

    ,

    /// <summary>
    ///     用户，按用户分别计数，匿名请求按客户端 IP 计数
    /// </summary>
    User = 2

    ,

    /// <summary>
    ///     角色，对拥有指定角色的用户生效，每个用户仍分别计数
    /// </summary>
    Role = 3
}