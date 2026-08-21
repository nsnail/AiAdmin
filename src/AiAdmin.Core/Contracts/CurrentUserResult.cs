// 定义登录和当前用户信息相关的数据传输模型。

using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户信息响应
/// </summary>
public sealed record CurrentUserResult(
    long UserId
    , string UserName
    , string Email
    , string Phone
    , UserGender Gender
    , string? Avatar
    , string[] Roles
    , string[] Buttons);