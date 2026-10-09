using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiAdmin.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace AiAdmin.Api.Services;

/// <summary>
///     JWT 访问令牌服务
/// </summary>
/// <param name="configuration">应用配置</param>
public sealed class TokenService(IConfiguration configuration)
{
    /// <summary>
    ///     JWT 中保存用户身份指纹的声明名称
    /// </summary>
    public const string IDENTITY_FINGERPRINT_CLAIM = "identity_fingerprint";

    /// <summary>
    ///     根据会影响 JWT 身份和权限的用户数据计算身份指纹
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="userName">登录用户名</param>
    /// <param name="passwordHash">密码哈希</param>
    /// <param name="roles">当前启用的角色编码</param>
    /// <returns>稳定的身份指纹</returns>
    public static string CreateIdentityFingerprint(
        long userId
        , string userName
        , string passwordHash
        , IEnumerable<string> roles
    ) {
        var normalizedRoles = string.Join('\n', roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        var source = $"{userId.ToString(CultureInfo.InvariantCulture)}\n{userName}\n{passwordHash}\n{normalizedRoles}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    /// <summary>
    ///     为用户创建包含角色声明的 JWT
    /// </summary>
    /// <param name="user">登录用户</param>
    /// <returns>JWT 字符串</returns>
    public string Create(User user) {
        var roles = user.UserRoles.Where(x => x.Role.IsEnabled).Select(x => x.Role.Code).Order(StringComparer.Ordinal).ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture))
            , new(JwtRegisteredClaimNames.UniqueName, user.UserName)
            , new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture))
            , new(ClaimTypes.Name, user.UserName)
            , new(IDENTITY_FINGERPRINT_CLAIM, CreateIdentityFingerprint(user.Id, user.UserName, user.PasswordHash, roles))
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