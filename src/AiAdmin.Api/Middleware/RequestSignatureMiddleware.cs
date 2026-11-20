using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     请求签名鉴权：系统设置开启后，所有已认证的接口请求必须携带有效的时间戳、一次性随机数和 HMAC 签名，
///     令牌泄露后攻击者没有签名密钥即无法构造请求，抓包重放则会被时间窗口和随机数去重拒绝
/// </summary>
/// <param name="next">后续请求处理委托</param>
public sealed class RequestSignatureMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     系统设置中开启请求签名的标签
    /// </summary>
    public const string SETTING_LABEL = "Enable request signature";

    private const int _MAX_NONCE_LENGTH = 64;
    private const int _MIN_NONCE_LENGTH = 16;

    /// <summary>
    ///     时间戳与服务器时间允许的最大偏差
    /// </summary>
    #pragma warning disable SA1202
    public static readonly TimeSpan TimestampWindow = TimeSpan.FromMinutes(5);
    #pragma warning restore SA1202

    /// <summary>
    ///     校验已认证请求的签名
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="dictionarySnapshotService">字典快照服务</param>
    /// <param name="nonceStore">随机数存储</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , DictionarySnapshotService dictionarySnapshotService
        , RequestNonceStore nonceStore
        , IConfiguration configuration
    ) {
        if (!IsSignatureRequired(context) || !await IsEnabledAsync(dictionarySnapshotService, context.RequestAborted).ConfigureAwait(false)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var failure = await ValidateAsync(context, nonceStore, configuration).ConfigureAwait(false);
        if (failure is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ApiResponse<object>(400, failure, null), context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    ///     计算请求体摘要，multipart 请求使用占位摘要，读取后复位流位置
    /// </summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>小写十六进制摘要或占位摘要</returns>
    private static async Task<string> ComputeBodyHashAsync(
        HttpRequest request
        , CancellationToken cancellationToken
    ) {
        if (request.HasFormContentType && request.ContentType!.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)) {
            return RequestSignature.UNSIGNED_PAYLOAD;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        await using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;
        return RequestSignature.HashBody(memory.GetBuffer().AsSpan(0, (int)memory.Length));
    }

    /// <summary>
    ///     读取系统设置中请求签名开关
    /// </summary>
    /// <param name="dictionarySnapshotService">字典快照服务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>开关已启用时返回 true</returns>
    private static async Task<bool> IsEnabledAsync(
        DictionarySnapshotService dictionarySnapshotService
        , CancellationToken cancellationToken
    ) {
        var settings = await dictionarySnapshotService
            .GetItemsAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE, cancellationToken)
            .ConfigureAwait(false);
        return settings.Any(x => x.Label == SETTING_LABEL && x is { Value: "true", IsEnabled: true });
    }

    /// <summary>
    ///     判断请求是否属于需要签名的范围：已认证的控制器接口，匿名接口与实时连接不要求
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <returns>需要签名时返回 true</returns>
    private static bool IsSignatureRequired(HttpContext context) {
        var endpoint = context.GetEndpoint();
        return context.User.Identity?.IsAuthenticated == true
               && !context.WebSockets.IsWebSocketRequest
               && endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is not null
               && endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() is null;
    }

    /// <summary>
    ///     判断时间戳是否在允许窗口内
    /// </summary>
    /// <param name="timestamp">Unix 毫秒时间戳文本</param>
    /// <returns>可解析且在窗口内时返回 true</returns>
    private static bool IsTimestampFresh(string timestamp) {
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)) {
            return false;
        }

        var skew = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return skew.Duration() <= TimestampWindow;
    }

    /// <summary>
    ///     按认证方式取得签名密钥：API Key 使用独立的签名 Secret，JWT 使用由会话标识派生的密钥
    /// </summary>
    /// <param name="user">当前用户</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>签名密钥，缺少会话标识时返回 null</returns>
    private static string? ResolveSigningKey(
        ClaimsPrincipal user
        , IConfiguration configuration
    ) {
        if (user.FindFirstValue(ApiKeyAuthenticationHandler.AUTH_METHOD_CLAIM) == ApiKeyAuthenticationHandler.SCHEME) {
            return user.FindFirstValue(ApiKeyAuthenticationHandler.SIGN_SECRET_CLAIM);
        }

        var sessionId = user.FindFirstValue(TokenService.SIGN_SESSION_CLAIM);
        return string.IsNullOrEmpty(sessionId) ? null : RequestSignature.DeriveSigningKey(configuration["Jwt:Key"]!, sessionId);
    }

    /// <summary>
    ///     校验签名头、时间窗口、签名值和随机数唯一性
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="nonceStore">随机数存储</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>校验通过返回 null，否则返回失败原因</returns>
    private static async Task<string?> ValidateAsync(
        HttpContext context
        , RequestNonceStore nonceStore
        , IConfiguration configuration
    ) {
        var headers = context.Request.Headers;
        var timestamp = headers[RequestSignature.TIMESTAMP_HEADER].ToString();
        var nonce = headers[RequestSignature.NONCE_HEADER].ToString();
        var signature = headers[RequestSignature.SIGNATURE_HEADER].ToString();
        if (timestamp.Length == 0 || signature.Length == 0 || nonce.Length is < _MIN_NONCE_LENGTH or > _MAX_NONCE_LENGTH) {
            return "Request signature is required";
        }

        if (!IsTimestampFresh(timestamp)) {
            return "Request timestamp is outside the allowed window";
        }

        var signingKey = ResolveSigningKey(context.User, configuration);
        if (signingKey is null) {
            return "Request signature is required";
        }

        var bodyHash = await ComputeBodyHashAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? context.Request.Path + context.Request.QueryString.Value;
        var expected = RequestSignature.Sign(
            signingKey, RequestSignature.BuildStringToSign(context.Request.Method, rawTarget, timestamp, nonce, bodyHash)
        );
        if (!RequestSignature.FixedTimeEquals(expected, signature)) {
            return "Request signature is invalid";
        }

        // 签名通过后才占用随机数，避免未授权方用伪造请求耗尽他人的随机数空间
        var scope = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var claimed = await nonceStore.TryClaimAsync(scope, nonce, TimestampWindow * 2).ConfigureAwait(false);
        return claimed ? null : "Request nonce has already been used";
    }
}