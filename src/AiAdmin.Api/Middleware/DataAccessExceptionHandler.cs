using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     处理数据权限不足异常
/// </summary>
public sealed class DataAccessExceptionHandler : IExceptionHandler
{
    /// <summary>
    ///     将数据权限不足转换为 403 响应
    /// </summary>
    /// <param name="httpContext">HTTP 请求上下文</param>
    /// <param name="exception">当前异常</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异常已处理时返回 true</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext
        , Exception exception
        , CancellationToken cancellationToken
    ) {
        if (exception is DynamicFilterValidationException dynamicFilterException) {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext
                .Response.WriteAsJsonAsync(new ApiResponse<object>(400, dynamicFilterException.Message, null), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (exception is not DataAccessDeniedException) {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext
            .Response.WriteAsJsonAsync(new ApiResponse<object>(403, "Data scope does not allow this operation", null), cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}