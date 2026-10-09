using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     执行基于角色接口映射的请求鉴权
/// </summary>
/// <param name="next">后续请求处理委托</param>
public sealed class ApiPermissionMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     校验当前请求的匿名、登录和接口权限状态
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="permissionCache">接口权限缓存</param>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , ApiPermissionCache permissionCache
        , AppDbContext db
    ) {
        // 所有控制器 API 默认执行统一权限检查，仅显式标记 AllowAnonymous 的端点例外。
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.AttributeRouteInfo?.Template is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
        var snapshot = await permissionCache.GetAsync(context.RequestAborted).ConfigureAwait(false);
        var apiKey = ApiEndpointKey.Create(context.Request.Method, action.AttributeRouteInfo.Template);
        if (snapshot.AnonymousKeys.Contains(apiKey)) {
            var metadata = endpoint!.Metadata.Concat([new AllowAnonymousAttribute()]);
            context.SetEndpoint(new Endpoint(endpoint.RequestDelegate, new EndpointMetadataCollection(metadata), endpoint.DisplayName));
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context
                .Response.WriteAsJsonAsync(new ApiResponse<object>(401, "Authentication is required", null), context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        // 每次授权请求都校验用户状态和身份指纹，使密码、角色及 JWT 身份内容变化后旧令牌立即失效
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId)) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context
                .Response.WriteAsJsonAsync(new ApiResponse<object>(401, "Login session has expired", null), context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        var currentIdentity = await db
            .Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsEnabled)
            .Select(x => new
                {
                    x.Id
                    , x.UserName
                    , x.PasswordHash
                    , Roles = x.UserRoles.Where(userRole => userRole.Role.IsEnabled).Select(userRole => userRole.Role.Code).ToArray()
                }
            )
            .SingleOrDefaultAsync(context.RequestAborted)
            .ConfigureAwait(false);
        var tokenFingerprint = context.User.FindFirstValue(TokenService.IDENTITY_FINGERPRINT_CLAIM);
        if (currentIdentity is null
            || string.IsNullOrWhiteSpace(tokenFingerprint)
            || !string.Equals(
                tokenFingerprint
                , TokenService.CreateIdentityFingerprint(
                    currentIdentity.Id, currentIdentity.UserName, currentIdentity.PasswordHash, currentIdentity.Roles
                ), StringComparison.Ordinal
            )) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context
                .Response.WriteAsJsonAsync(new ApiResponse<object>(401, "Login session has expired", null), context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        if (roles.Contains("R_SUPER", StringComparer.Ordinal) || snapshot.Allows(roles, apiKey)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context
            .Response.WriteAsJsonAsync(new ApiResponse<object>(403, "No permission to access this API", null), context.RequestAborted)
            .ConfigureAwait(false);
    }
}