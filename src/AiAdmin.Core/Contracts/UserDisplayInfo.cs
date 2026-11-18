namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表里展示用户（如分配的商家、回调所属商家）所需的用户信息，由列表接口批量从用户表取回
/// </summary>
/// <param name="UserName">用户名</param>
/// <param name="Avatar">头像地址，未设置头像时为空</param>
public sealed record UserDisplayInfo(string UserName, string? Avatar);