using System.Security.Cryptography;
using System.Text;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     提供用户登录接口
/// </summary>
[ApiController]
[ApiDescription("Authentication")]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, TokenService tokenService, IMemoryCache cache) : ControllerBase
{
    private const int ProofDifficulty = 4;
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     创建登录算力挑战
    /// </summary>
    /// <returns>算力挑战和难度</returns>
    [HttpGet("challenge")]
    [AllowAnonymous]
    [ApiDescription("Create login proof challenge")]
    public ActionResult<ApiResponse<LoginChallengeResult>> ChallengeEndpoint() {
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _ = cache.Set($"login-proof:{challenge}", true, ChallengeLifetime);
        return Ok(ApiResponse<LoginChallengeResult>.Ok(new LoginChallengeResult(challenge, ProofDifficulty)));
    }

    /// <summary>
    ///     查询登录页面配置
    /// </summary>
    /// <returns>登录页面配置</returns>
    [HttpGet("config")]
    [AllowAnonymous]
    [ApiDescription("Query login configuration")]
    public async Task<ActionResult<ApiResponse<LoginConfigResult>>> ConfigAsync() {
        var enabled = await db
            .DictionaryItems.AnyAsync(x => x.Category.Code == "system_settings"
                                           && x.Label == "Enable login slider verification"
                                           && x.Value == "true"
                                           && x.IsEnabled
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<LoginConfigResult>.Ok(new LoginConfigResult(enabled)));
    }

    /// <summary>
    ///     校验账号密码并签发访问令牌
    /// </summary>
    /// <param name="request">登录凭据</param>
    /// <returns>登录结果或失败响应</returns>
    [HttpPost("login")]
    [ApiDescription("Sign in to the system")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> LoginAsync(LoginRequest request) {
        if (!cache.TryGetValue($"login-proof:{request.Challenge}", out _) || !IsValidProof(request.Challenge, request.Proof, ProofDifficulty)) {
            return Unauthorized(new ApiResponse<object>(401, ApiMessages.Get(Request, "invalidCredentials"), null));
        }

        cache.Remove($"login-proof:{request.Challenge}");

        // 校验用户状态和密码，成功后返回带角色声明的令牌。
        var user = await db
            .Users.Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.UserName == request.UserName)
            .ConfigureAwait(false);
        return user?.IsEnabled != true || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)
            ? Unauthorized(new ApiResponse<object>(401, ApiMessages.Get(Request, "invalidCredentials"), null))
            : Ok(ApiResponse<LoginResult>.Ok(new LoginResult(tokenService.Create(user), string.Empty), ApiMessages.Get(Request, "loginSuccess")));
    }

    private static bool IsValidProof(
        string challenge
        , string proof
        , int difficulty
    ) {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > 32) {
            return false;
        }

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge}:{proof}")));
        return digest.StartsWith(new string('0', difficulty), StringComparison.OrdinalIgnoreCase);
    }
}