using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     令牌续签：访问令牌临近到期时客户端在请求头携带续签令牌，服务端校验通过后在响应头一次性下发新的访问令牌和续签令牌。
///     续签令牌平时不传输；续签失败不影响本次请求，访问令牌仍按原有效期工作
/// </summary>
/// <param name="next">后续请求处理委托</param>
public sealed class TokenRenewalMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     识别并准备续签，实际换发推迟到响应开始时，保证只有处理成功的请求才消耗续签令牌
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="tokenService">令牌服务</param>
    /// <param name="db">数据库上下文</param>
    /// <param name="nonceStore">一次性标识存储</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , TokenService tokenService
        , AppDbContext db
        , RequestNonceStore nonceStore
    ) {
        var plan = await PlanRotationAsync(context, tokenService, db).ConfigureAwait(false);
        if (plan is not null) {
            context.Response.OnStarting(() => ApplyRotationAsync(context.Response, plan.Value, tokenService, nonceStore));
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    ///     响应开始时换发令牌：处理失败（5xx）的请求不消耗续签令牌，续签令牌用 SET NX 占用保证只能使用一次
    /// </summary>
    /// <param name="response">HTTP 响应</param>
    /// <param name="plan">续签内容</param>
    /// <param name="tokenService">令牌服务</param>
    /// <param name="nonceStore">一次性标识存储</param>
    /// <returns>异步换发任务</returns>
    private static async Task ApplyRotationAsync(
        HttpResponse response
        , (RefreshTicket Ticket, string UserName, string[] Roles) plan
        , TokenService tokenService
        , RequestNonceStore nonceStore
    ) {
        if (response.StatusCode >= StatusCodes.Status500InternalServerError) {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var ttl = plan.Ticket.ExpiresAt - now;
        if (ttl <= TimeSpan.Zero || !await nonceStore.TryClaimAsync("refresh-token", plan.Ticket.TokenId, ttl).ConfigureAwait(false)) {
            return;
        }

        var rotated = tokenService.Rotate(plan.Ticket, plan.UserName, plan.Roles, now);
        if (rotated is null) {
            return;
        }

        response.Headers[TokenService.NEW_TOKEN_HEADER] = rotated.Value.Token;
        response.Headers[TokenService.NEW_REFRESH_TOKEN_HEADER] = rotated.Value.RefreshToken;
    }

    /// <summary>
    ///     校验续签令牌与当前访问令牌属于同一用户和会话，且用户身份指纹未变化
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="tokenService">令牌服务</param>
    /// <param name="db">数据库上下文</param>
    /// <returns>可续签时返回续签内容，否则返回 null</returns>
    private static async Task<(RefreshTicket Ticket, string UserName, string[] Roles)?> PlanRotationAsync(
        HttpContext context
        , TokenService tokenService
        , AppDbContext db
    ) {
        var refreshToken = context.Request.Headers[TokenService.REFRESH_TOKEN_HEADER].ToString();
        var user = context.User;
        if (refreshToken.Length == 0
            || user.Identity?.IsAuthenticated != true
            || user.FindFirstValue(ApiKeyAuthenticationHandler.AUTH_METHOD_CLAIM) == ApiKeyAuthenticationHandler.SCHEME) {
            return null;
        }

        var ticket = tokenService.ReadRefreshToken(refreshToken);
        if (ticket is null
            || user.FindFirstValue(ClaimTypes.NameIdentifier) != ticket.UserId.ToString(CultureInfo.InvariantCulture)
            || user.FindFirstValue(TokenService.SIGN_SESSION_CLAIM) != ticket.SessionId) {
            return null;
        }

        var identity = await db
            .Users.AsNoTracking()
            .Where(x => x.Id == ticket.UserId && x.IsEnabled)
            .Select(x => new
                {
                    x.UserName
                    , x.PasswordHash
                    , Roles = x.UserRoles.Where(userRole => userRole.Role.IsEnabled).Select(userRole => userRole.Role.Code).ToArray()
                }
            )
            .SingleOrDefaultAsync(context.RequestAborted)
            .ConfigureAwait(false);
        return identity is not null
               && TokenService.CreateIdentityFingerprint(ticket.UserId, identity.UserName, identity.PasswordHash, identity.Roles)
               == ticket.Fingerprint
            ? (ticket, identity.UserName, identity.Roles)
            : null;
    }
}