using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     当前用户 API Key 管理控制器，仅允许 JWT 调用
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="credentialProtectionService">凭据加解密服务</param>
[ApiController]
[ApiDescription("API key management")]
[Route("api/user/api-key")]
public sealed class ApiKeyController(AppDbContext db, CredentialProtectionService credentialProtectionService) : ControllerBase
{
    /// <summary>
    ///     吊销当前用户 API Key
    /// </summary>
    /// <returns>吊销结果</returns>
    [HttpPost("delete")]
    [JwtOnly]
    [ApiDescription("Revoke current user API key")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync() {
        var userId = GetUserId();
        _ = await db.UserApiKeys.Where(x => x.UserId == userId).ExecuteDeleteAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "API key revoked"));
    }

    /// <summary>
    ///     查询当前用户 API Key 状态
    /// </summary>
    /// <returns>API Key 状态，不含明文</returns>
    [HttpGet]
    [JwtOnly]
    [ApiDescription("Get current user API key status")]
    public async Task<ActionResult<ApiResponse<ApiKeyStatusResult>>> GetAsync() {
        var userId = GetUserId();
        var record = await db
            .UserApiKeys.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<ApiKeyStatusResult>.Ok(ToResult(record, null, null)));
    }

    /// <summary>
    ///     生成或重置当前用户 API Key，旧 Key 立即失效
    /// </summary>
    /// <returns>包含一次性明文的 API Key 状态</returns>
    [HttpPost("reset")]
    [JwtOnly]
    [ApiDescription("Generate or reset current user API key")]
    public async Task<ActionResult<ApiResponse<ApiKeyStatusResult>>> ResetAsync() {
        var userId = GetUserId();
        var apiKey = ApiKeyService.Generate();
        var hash = ApiKeyService.Hash(apiKey);
        var secret = ApiKeyService.GenerateSecret();
        var protectedSecret = credentialProtectionService.Protect(secret);
        var record = await db.UserApiKeys.SingleOrDefaultAsync(x => x.UserId == userId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (record is null) {
            record = new UserApiKey
            {
                UserId = userId, KeyHash = hash, KeyPrefix = ApiKeyService.GetDisplayPrefix(apiKey), SignSecret = protectedSecret
            };
            _ = await db.UserApiKeys.AddAsync(record, HttpContext.RequestAborted).ConfigureAwait(false);
        }
        else {
            // 覆盖哈希即让旧 Key 失效，Secret 同步更换
            record.KeyHash = hash;
            record.SignSecret = protectedSecret;
            record.KeyPrefix = ApiKeyService.GetDisplayPrefix(apiKey);
            record.CreatedAt = DateTime.UtcNow;
            record.LastUsedAt = null;
        }

        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<ApiKeyStatusResult>.Ok(ToResult(record, apiKey, secret), "API key generated"));
    }

    /// <summary>
    ///     转换为状态响应
    /// </summary>
    /// <param name="record">API Key 记录</param>
    /// <param name="plainKey">一次性明文</param>
    /// <param name="plainSecret">一次性明文签名 Secret</param>
    /// <returns>状态响应</returns>
    private static ApiKeyStatusResult ToResult(
        UserApiKey? record
        , string? plainKey
        , string? plainSecret
    ) {
        return record is null
            ? new ApiKeyStatusResult(false, string.Empty, null, null, null, null)
            : new ApiKeyStatusResult(
                true, record.KeyPrefix, ServerTime.ToOffset(record.CreatedAt)
                , record.LastUsedAt is null ? null : ServerTime.ToOffset(record.LastUsedAt.Value), plainKey, plainSecret
            );
    }

    /// <summary>
    ///     读取当前登录用户主键
    /// </summary>
    /// <returns>用户主键</returns>
    private long GetUserId() {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }
}