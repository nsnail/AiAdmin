// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     发送注册邮箱验证码请求
/// </summary>
/// <param name="Email">电子邮箱</param>
/// <param name="PuzzleTicket">拼图校验凭证</param>
public sealed record RegisterCodeRequest(string Email, string PuzzleTicket);