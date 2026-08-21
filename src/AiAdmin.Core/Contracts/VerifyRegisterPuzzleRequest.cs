// 定义登录和当前用户信息相关的数据传输模型。

using System.Text.Json.Serialization;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     拼图校验请求
/// </summary>
/// <param name="ChallengeId">挑战主键</param>
/// <param name="OffsetX">拼图横向偏移</param>
/// <param name="Email">接收验证码的电子邮箱</param>
public sealed record VerifyRegisterPuzzleRequest(
    string ChallengeId
    , [property: JsonRequired]
    int OffsetX
    , string Email);