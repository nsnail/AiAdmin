using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiAdmin.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace AiAdmin.Api.Services;

/// <summary>
///     JWT 访问令牌与续签令牌服务：访问令牌短时效、常规携带；续签令牌长时效、平时不传输，
///     只在访问令牌临近到期时携带，服务端校验后一次性换发新的两个令牌
/// </summary>
/// <param name="configuration">应用配置</param>
public sealed class TokenService(IConfiguration configuration)
{
    /// <summary>
    ///     JWT 中保存用户身份指纹的声明名称
    /// </summary>
    public const string IDENTITY_FINGERPRINT_CLAIM = "identity_fingerprint";

    /// <summary>
    ///     续签成功后下发新续签令牌的响应头
    /// </summary>
    public const string NEW_REFRESH_TOKEN_HEADER = "X-New-Refresh-Token";

    /// <summary>
    ///     续签成功后下发新访问令牌的响应头
    /// </summary>
    public const string NEW_TOKEN_HEADER = "X-New-Token";

    /// <summary>
    ///     客户端临近到期时携带续签令牌的请求头
    /// </summary>
    public const string REFRESH_TOKEN_HEADER = "X-Refresh-Token";

    /// <summary>
    ///     JWT 中保存请求签名会话标识的声明名称
    /// </summary>
    public const string SIGN_SESSION_CLAIM = "sign_sid";

    private const string _REFRESH_TYPE = "refresh";
    private const string _SESSION_START_CLAIM = "sess_start";
    private const string _TOKEN_TYPE_CLAIM = "token_type";

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
    ///     为用户创建新会话的访问令牌、续签令牌和请求签名密钥
    /// </summary>
    /// <param name="user">登录用户</param>
    /// <returns>访问令牌、续签令牌和请求签名密钥</returns>
    public (string Token, string RefreshToken, string SignKey) Create(User user) {
        var roles = user.UserRoles.Where(x => x.Role.IsEnabled).Select(x => x.Role.Code).Order(StringComparer.Ordinal).ToArray();
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var fingerprint = CreateIdentityFingerprint(user.Id, user.UserName, user.PasswordHash, roles);
        var now = DateTimeOffset.UtcNow;
        var (token, refreshToken) = IssuePair(user.Id, user.UserName, roles, sessionId, fingerprint, now, now);
        return (token, refreshToken, CreateSigningKey(sessionId));
    }

    /// <summary>
    ///     根据 JWT 中的签名会话标识派生请求签名密钥，密钥只在登录响应中下发一次
    /// </summary>
    /// <param name="sessionId">签名会话标识</param>
    /// <returns>小写十六进制签名密钥</returns>
    public string CreateSigningKey(string sessionId) {
        return RequestSignature.DeriveSigningKey(configuration["Jwt:Key"]!, sessionId);
    }

    /// <summary>
    ///     校验续签令牌的签名、受众、类型和有效期，不检查它是否已被使用
    /// </summary>
    /// <param name="refreshToken">客户端携带的续签令牌</param>
    /// <returns>校验通过的令牌内容，无效时返回 null</returns>
    public RefreshTicket? ReadRefreshToken(string refreshToken) {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true
            , ValidIssuer = configuration["Jwt:Issuer"]
            , ValidateAudience = true
            , ValidAudience = GetRefreshAudience()
            , ValidateLifetime = true
            , ValidateIssuerSigningKey = true
            , IssuerSigningKey = CreateKey()
            , ClockSkew = TimeSpan.Zero
        };
        try {
            var principal = handler.ValidateToken(refreshToken, parameters, out var validated);
            var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var sessionId = principal.FindFirstValue(SIGN_SESSION_CLAIM);
            var fingerprint = principal.FindFirstValue(IDENTITY_FINGERPRINT_CLAIM);
            var tokenId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            if (principal.FindFirstValue(_TOKEN_TYPE_CLAIM) != _REFRESH_TYPE
                || !long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                || !long.TryParse(
                    principal.FindFirstValue(_SESSION_START_CLAIM), NumberStyles.None, CultureInfo.InvariantCulture, out var startSeconds
                )
                || string.IsNullOrEmpty(sessionId)
                || string.IsNullOrEmpty(fingerprint)
                || string.IsNullOrEmpty(tokenId)) {
                return null;
            }

            return new RefreshTicket(
                userId, sessionId, fingerprint, DateTimeOffset.FromUnixTimeSeconds(startSeconds), new DateTimeOffset(validated.ValidTo, TimeSpan.Zero)
                , tokenId
            );
        }
        catch (SecurityTokenException) {
            return null;
        }
        catch (ArgumentException) {
            return null;
        }
    }

    /// <summary>
    ///     使用有效的续签令牌换发新的访问令牌和续签令牌，沿用原会话标识因此请求签名密钥不变，
    ///     新续签令牌的到期时间不会超过会话首次登录时间加上最长会话时长
    /// </summary>
    /// <param name="ticket">校验通过的续签令牌内容</param>
    /// <param name="userName">登录用户名</param>
    /// <param name="roles">当前启用的角色编码</param>
    /// <param name="now">当前时间</param>
    /// <returns>新的访问令牌和续签令牌，会话已达最长时长时返回 null</returns>
    public (string Token, string RefreshToken)? Rotate(
        RefreshTicket ticket
        , string userName
        , IEnumerable<string> roles
        , DateTimeOffset now
    ) {
        var sessionEnd = ticket.SessionStart.AddMinutes(configuration.GetValue("Jwt:MaxSessionMinutes", 43200));
        return now >= sessionEnd ? null : IssuePair(ticket.UserId, userName, roles, ticket.SessionId, ticket.Fingerprint, ticket.SessionStart, now);
    }

    /// <summary>
    ///     创建 JWT 对称签名密钥
    /// </summary>
    /// <returns>对称签名密钥</returns>
    private SymmetricSecurityKey CreateKey() {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
    }

    /// <summary>
    ///     读取续签令牌受众，与访问令牌受众不同，使续签令牌无法作为访问令牌使用
    /// </summary>
    /// <returns>续签令牌受众</returns>
    private string GetRefreshAudience() {
        return configuration["Jwt:Audience"] + ".refresh";
    }

    /// <summary>
    ///     签发一对访问令牌和续签令牌，续签令牌到期时间受会话最长时长限制
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="userName">登录用户名</param>
    /// <param name="roles">当前启用的角色编码</param>
    /// <param name="sessionId">签名会话标识</param>
    /// <param name="fingerprint">用户身份指纹</param>
    /// <param name="sessionStart">会话首次登录时间</param>
    /// <param name="now">当前时间</param>
    /// <returns>访问令牌和续签令牌</returns>
    private (string Token, string RefreshToken) IssuePair(
        long userId
        , string userName
        , IEnumerable<string> roles
        , string sessionId
        , string fingerprint
        , DateTimeOffset sessionStart
        , DateTimeOffset now
    ) {
        var id = userId.ToString(CultureInfo.InvariantCulture);
        List<Claim> accessClaims =
        [
            new(JwtRegisteredClaimNames.Sub, id), new(JwtRegisteredClaimNames.UniqueName, userName), new(ClaimTypes.NameIdentifier, id)
            , new(ClaimTypes.Name, userName), new(IDENTITY_FINGERPRINT_CLAIM, fingerprint), new(SIGN_SESSION_CLAIM, sessionId)
            , .. roles.Select(role => new Claim(ClaimTypes.Role, role))
        ];
        var accessExpires = now.AddMinutes(configuration.GetValue("Jwt:ExpiresMinutes", 120));
        var sessionEnd = sessionStart.AddMinutes(configuration.GetValue("Jwt:MaxSessionMinutes", 43200));
        var refreshExpires = now.AddMinutes(configuration.GetValue("Jwt:RefreshExpiresMinutes", 10080));
        if (refreshExpires > sessionEnd) {
            refreshExpires = sessionEnd;
        }

        // 访问令牌不能超过续签令牌的有效期，否则会话到达最长时长后访问令牌仍然可用
        if (accessExpires > refreshExpires) {
            accessExpires = refreshExpires;
        }

        List<Claim> refreshClaims =
        [
            new(JwtRegisteredClaimNames.Sub, id), new(JwtRegisteredClaimNames.Jti, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
            , new(_TOKEN_TYPE_CLAIM, _REFRESH_TYPE), new(SIGN_SESSION_CLAIM, sessionId), new(IDENTITY_FINGERPRINT_CLAIM, fingerprint)
            , new(_SESSION_START_CLAIM, sessionStart.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ];
        return (Write(configuration["Jwt:Audience"], accessClaims, accessExpires), Write(GetRefreshAudience(), refreshClaims, refreshExpires));
    }

    /// <summary>
    ///     使用服务端密钥签发 JWT
    /// </summary>
    /// <param name="audience">令牌受众</param>
    /// <param name="claims">声明集合</param>
    /// <param name="expires">到期时间</param>
    /// <returns>JWT 字符串</returns>
    private string Write(
        string? audience
        , IEnumerable<Claim> claims
        , DateTimeOffset expires
    ) {
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"], audience, claims, expires: expires.UtcDateTime
            , signingCredentials: new SigningCredentials(CreateKey(), SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}