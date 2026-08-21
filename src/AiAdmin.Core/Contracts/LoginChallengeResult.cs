// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录算力挑战
/// </summary>
public sealed record LoginChallengeResult(string Challenge, int Difficulty);