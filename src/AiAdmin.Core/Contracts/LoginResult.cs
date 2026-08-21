// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录令牌响应
/// </summary>
public sealed record LoginResult(string Token, string RefreshToken);