using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     接入实时 WebSocket 连接，必须置于认证中间件之后、HTTP 日志中间件之前，避免响应体被缓冲影响协议升级
/// </summary>
/// <param name="next">下一个请求处理委托</param>
public sealed class RealtimeWebSocketMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     WebSocket 端点路径
    /// </summary>
    public const string PATH = "/api/ws";

    /// <summary>
    ///     路径匹配时校验登录并升级为 WebSocket，否则继续执行请求管道
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="manager">实时连接管理器</param>
    /// <param name="permissionCache">接口权限缓存，用于判断连接用户是否是客服</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , RealtimeConnectionManager manager
        , ApiPermissionCache permissionCache
    ) {
        if (!context.Request.Path.Equals(PATH, StringComparison.OrdinalIgnoreCase)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true
            || !long.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId
            )) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 拥有客服回复接口权限的用户视为客服，超级管理员默认拥有全部权限
        var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
        var snapshot = await permissionCache.GetAsync(context.RequestAborted).ConfigureAwait(false);
        var isService = roles.Contains("R_SUPER", StringComparer.Ordinal)
                        || snapshot.Allows(roles, ApiEndpointKey.Create("POST", "api/support/reply"));
        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await manager.HandleAsync(socket, userId, isService, context.RequestAborted).ConfigureAwait(false);
    }
}