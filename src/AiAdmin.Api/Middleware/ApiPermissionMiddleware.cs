using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

// 在 ASP.NET 授权中间件前执行接口级权限校验和匿名接口放行。
namespace AiAdmin.Api.Middleware;

/// <summary>
///     执行基于角色接口映射的请求鉴权
/// </summary>
public sealed class ApiPermissionMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     校验当前请求的匿名、登录和接口权限状态
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="permissionCache">接口权限缓存</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , ApiPermissionCache permissionCache
    ) {
        // 仅处理标记 Authorize 的控制器动作，其他端点直接进入后续管道。
        var endpoint = context.GetEndpoint();
        var requiresAuthorization = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
        if (!requiresAuthorization) {
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