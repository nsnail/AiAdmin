using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     安全停机闸门中间件：登记在途请求；闸门关闭后拒绝所有新请求（含 WebSocket 握手），停机管理接口除外
/// </summary>
/// <param name="next">下一个请求处理委托</param>
public sealed class ShutdownGateMiddleware(RequestDelegate next)
{
    private static readonly PathString _shutdownPath = new("/api/system/shutdown");

    /// <summary>
    ///     先登记再检查闸门，避免请求在检查与登记之间漏过停机判定；闸门关闭时直接返回 503
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(HttpContext context) {
        // 停机管理接口自身不计入在途，否则 prepare 会永远等待自己
        if (context.Request.Path.StartsWithSegments(_shutdownPath, StringComparison.OrdinalIgnoreCase)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        using var lease = ShutdownGate.Enter(ShutdownActivity.Http);
        if (!ShutdownGate.IsClosed) {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "30";
        await context.Response.WriteAsJsonAsync(new ApiResponse<object>(503, "Service is shutting down", null), context.RequestAborted).ConfigureAwait(false);
    }
}