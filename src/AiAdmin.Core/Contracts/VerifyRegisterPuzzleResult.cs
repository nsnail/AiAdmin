namespace AiAdmin.Api.Contracts;

/// <summary>
///     拼图校验结果
/// </summary>
/// <param name="PuzzleTicket">拼图验证票据</param>
public sealed record VerifyRegisterPuzzleResult(string PuzzleTicket);