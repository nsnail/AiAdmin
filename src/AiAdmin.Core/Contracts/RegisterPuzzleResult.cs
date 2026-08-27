namespace AiAdmin.Api.Contracts;

/// <summary>
///     邮箱验证码拼图挑战
/// </summary>
/// <param name="ChallengeId">挑战标识</param>
/// <param name="BackgroundImage">背景图片</param>
/// <param name="PieceImage">拼图图片</param>
/// <param name="Width">图片宽度</param>
/// <param name="Height">图片高度</param>
/// <param name="PieceSize">拼图尺寸</param>
/// <param name="PieceY">拼图纵坐标</param>
public sealed record RegisterPuzzleResult(
    string ChallengeId
    , string BackgroundImage
    , string PieceImage
    , int Width
    , int Height
    , int PieceSize
    , int PieceY);