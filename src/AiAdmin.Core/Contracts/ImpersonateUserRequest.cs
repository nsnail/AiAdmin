namespace AiAdmin.Api.Contracts;

/// <summary>
///     模拟登录请求
/// </summary>
/// <param name="Id">被模拟的用户主键</param>
public sealed record ImpersonateUserRequest(long Id);