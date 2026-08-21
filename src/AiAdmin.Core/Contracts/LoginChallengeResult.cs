namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录算力挑战
/// </summary>
public sealed record LoginChallengeResult(string Challenge, int Difficulty);