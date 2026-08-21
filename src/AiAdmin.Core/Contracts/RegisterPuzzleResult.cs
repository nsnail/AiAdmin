// 定义登录和当前用户信息相关的数据传输模型。

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