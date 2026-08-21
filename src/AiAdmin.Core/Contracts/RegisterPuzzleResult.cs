namespace AiAdmin.Api.Contracts;

/// <summary>
///     邮箱验证码拼图挑战
/// </summary>
public sealed record RegisterPuzzleResult(
    string ChallengeId
    , string BackgroundImage
    , string PieceImage
    , int Width
    , int Height
    , int PieceSize
    , int PieceY);