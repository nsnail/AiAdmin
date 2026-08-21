// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     拼图校验结果
/// </summary>
public sealed record VerifyRegisterPuzzleResult(string PuzzleTicket);