using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiAdmin.Api.Models;
using Microsoft.IdentityModel.Tokens;

// 根据用户身份和角色生成用于 Bearer 鉴权的 JWT。
namespace AiAdmin.Api.Services;

/// <summary>
///     JWT 访问令牌服务
/// </summary>
public sealed class TokenService(IConfiguration configuration)
{
    /// <summary>
    ///     为用户创建包含角色声明的 JWT
    /// </summary>
    /// <param name="user">登录用户</param>
    /// <returns>JWT 字符串</returns>
    public string Create(User user) {
        var roles = user.UserRoles.Select(x => x.Role.Code).ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture))
            , new(JwtRegisteredClaimNames.UniqueName, user.UserName)
            , new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture))
            , new(ClaimTypes.Name, user.UserName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims
            , expires: DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:ExpiresMinutes", 120))
            , signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}