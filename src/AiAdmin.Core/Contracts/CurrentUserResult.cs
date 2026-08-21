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