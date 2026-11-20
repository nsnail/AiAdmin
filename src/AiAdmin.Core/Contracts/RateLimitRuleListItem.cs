using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     限流规则列表项
/// </summary>
/// <param name="Id">规则主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="ApiEndpointId">接口主键</param>
/// <param name="Method">接口请求方法</param>
/// <param name="Path">接口路径</param>
/// <param name="ApiName">接口名称</param>
/// <param name="Algorithm">限流算法</param>
/// <param name="Scope">限流范围</param>
/// <param name="RoleCode">指定的角色编码</param>
/// <param name="RoleName">指定的角色名称，角色已被删除时为空</param>
/// <param name="UserId">指定的用户主键</param>
/// <param name="UserName">指定的用户名，用户已被删除时为空</param>
/// <param name="Limit">窗口时间内允许的请求数</param>
/// <param name="WindowSeconds">统计窗口秒数</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="Remark">备注</param>
/// <param name="UpdatedAt">最后更新时间</param>
public sealed record RateLimitRuleListItem(
    long Id
    , DateTimeOffset CreatedAt
    , long ApiEndpointId
    , string Method
    , string Path
    , string ApiName
    , RateLimitAlgorithm Algorithm
    , RateLimitScope Scope
    , string? RoleCode
    , string? RoleName
    , long? UserId
    , string? UserName
    , int Limit
    , int WindowSeconds
    , bool IsEnabled
    , string Remark
    , DateTimeOffset? UpdatedAt);