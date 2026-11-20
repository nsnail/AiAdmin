using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Services;

/// <summary>
///     API Key 认证处理器，读取请求头中的 API Key 并按当前用户数据构造与 JWT 等价的身份
/// </summary>
/// <param name="options">认证方案选项</param>
/// <param name="logger">日志工厂</param>
/// <param name="encoder">URL 编码器</param>
/// <param name="db">应用数据库上下文</param>
/// <param name="credentialProtectionService">凭据加解密服务</param>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options
    , ILoggerFactory logger
    , UrlEncoder encoder
    , AppDbContext db
    , CredentialProtectionService credentialProtectionService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    ///     标识本次请求通过 API Key 认证的声明名称
    /// </summary>
    public const string AUTH_METHOD_CLAIM = "auth_method";

    /// <summary>
    ///     API Key 认证方案名称
    /// </summary>
    public const string SCHEME = "ApiKey";

    /// <summary>
    ///     保存签名 Secret 明文的声明名称，只存在于单次请求的内存身份中，请求签名时用作签名密钥
    /// </summary>
    public const string SIGN_SECRET_CLAIM = "api_key_sign_secret";

    /// <summary>
    ///     最后使用时间的最小写入间隔
    /// </summary>
    private static readonly TimeSpan _lastUsedWriteInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     判断请求是否携带 API Key
    /// </summary>
    /// <param name="request">HTTP 请求</param>
    /// <returns>携带 API Key 时返回 true</returns>
    public static bool HasApiKey(HttpRequest request) {
        return !string.IsNullOrWhiteSpace(ReadApiKey(request));
    }

    /// <summary>
    ///     校验 API Key 并构造用户身份
    /// </summary>
    /// <returns>认证结果</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
        var apiKey = ReadApiKey(Request);
        if (string.IsNullOrWhiteSpace(apiKey)) {
            return AuthenticateResult.NoResult();
        }

        var hash = ApiKeyService.Hash(apiKey);
        var record = await db.UserApiKeys.Where(x => x.KeyHash == hash).SingleOrDefaultAsync(Context.RequestAborted).ConfigureAwait(false);
        if (record is null) {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var user = await db
            .Users.AsNoTracking()
            .Where(x => x.Id == record.UserId && x.IsEnabled)
            .Select(x => new { x.Id, x.UserName, x.PasswordHash, Roles = x.UserRoles.Where(r => r.Role.IsEnabled).Select(r => r.Role.Code).ToList() })
            .SingleOrDefaultAsync(Context.RequestAborted)
            .ConfigureAwait(false);
        if (user is null) {
            return AuthenticateResult.Fail("Invalid API key");
        }

        await TouchLastUsedAsync(record).ConfigureAwait(false);
        var userId = user.Id.ToString(CultureInfo.InvariantCulture);
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, user.UserName)
            , new(
                TokenService.IDENTITY_FINGERPRINT_CLAIM
                , TokenService.CreateIdentityFingerprint(user.Id, user.UserName, user.PasswordHash, user.Roles)
            )
            , new(AUTH_METHOD_CLAIM, SCHEME), .. user.Roles.Select(role => new Claim(ClaimTypes.Role, role))
        ];

        // Unprotect 对无法解密的内容会原样返回，只有以 Secret 前缀开头才视为有效明文，避免把密文当作签名密钥
        var signSecret = credentialProtectionService.Unprotect(record.SignSecret);
        if (signSecret.StartsWith(ApiKeyService.SECRET_PREFIX, StringComparison.Ordinal)) {
            claims.Add(new Claim(SIGN_SECRET_CLAIM, signSecret));
        }

        var identity = new ClaimsIdentity(claims, SCHEME);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SCHEME));
    }

    /// <summary>
    ///     从请求头读取 API Key
    /// </summary>
    /// <param name="request">HTTP 请求</param>
    /// <returns>API Key，未携带时返回 null</returns>
    private static string? ReadApiKey(HttpRequest request) {
        var header = request.Headers[ApiKeyService.HEADER_NAME].ToString();
        if (!string.IsNullOrWhiteSpace(header)) {
            return header.Trim();
        }

        var authorization = request.Headers.Authorization.ToString();
        const string prefix = ApiKeyService.AUTHORIZATION_SCHEME + " ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? authorization[prefix.Length..].Trim() : null;
    }

    /// <summary>
    ///     按节流间隔更新最后使用时间
    /// </summary>
    /// <param name="record">API Key 记录</param>
    /// <returns>异步更新任务</returns>
    private async Task TouchLastUsedAsync(UserApiKey record) {
        var now = DateTime.UtcNow;
        if (record.LastUsedAt is not null && now - record.LastUsedAt < _lastUsedWriteInterval) {
            return;
        }

        record.LastUsedAt = now;
        _ = await db.SaveChangesAsync(Context.RequestAborted).ConfigureAwait(false);
    }
}