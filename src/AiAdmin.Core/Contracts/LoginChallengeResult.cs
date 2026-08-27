namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录算力挑战
/// </summary>
/// <param name="Challenge">挑战内容</param>
/// <param name="Difficulty">挑战难度</param>
public sealed record LoginChallengeResult(string Challenge, int Difficulty);