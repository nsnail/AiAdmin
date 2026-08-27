using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户信息响应
/// </summary>
/// <param name="UserId">用户主键</param>
/// <param name="UserName">登录用户名</param>
/// <param name="Email">电子邮箱</param>
/// <param name="Phone">联系电话</param>
/// <param name="Gender">性别编码</param>
/// <param name="Avatar">头像地址</param>
/// <param name="Roles">角色编码集合</param>
/// <param name="Buttons">权限按钮集合</param>
/// <param name="Version">并发版本号</param>
public sealed record CurrentUserResult(
    long UserId
    , string UserName
    , string Email
    , string Phone
    , UserGender Gender
    , string? Avatar
    , string[] Roles
    , string[] Buttons
    , int Version);