using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Filters;

/// <summary>
///     标记接口支持幂等。只有请求带 Idempotency-Key 头时才生效：相同的键在保留期内直接回放首次成功的响应；
///     不带该头的请求（外部 API 调用、旧客户端）完全不受影响。仅成功（2xx）的响应会被保存，失败后可用同一个键重试。
///     幂等存储（Redis）故障时放行请求，不影响业务
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : ActionFilterAttribute
{
    /// <summary>
    ///     幂等键请求头名称
    /// </summary>
    public const string HEADER_NAME = "Idempotency-Key";

    /// <summary>
    ///     回放响应时附带的响应头名称
    /// </summary>
    public const string REPLAY_HEADER_NAME = "Idempotent-Replayed";

    private const int _LOCK_SECONDS = 60;

    private const int _MAX_KEY_LENGTH = 128;

    /// <summary>
    ///     HTTP 425：相同的请求仍在处理中，ASP.NET Core 没有对应常量
    /// </summary>
    private const int _STATUS_TOO_EARLY = 425;

    /// <summary>
    ///     幂等存储故障时的日志委托
    /// </summary>
    private static readonly Action<ILogger, Exception?> _logStoreFailure = LoggerMessage.Define(
        LogLevel.Warning, new EventId(3501, "IdempotencyStoreFailure")
        , "Idempotency store is unavailable, the request is processed without idempotency"
    );

    /// <summary>
    ///     获取成功响应的保留秒数，默认 24 小时
    /// </summary>
    public int ReplaySeconds { get; init; } = 24 * 60 * 60;

    /// <summary>
    ///     执行幂等检查：重复请求直接返回结果，首次请求执行业务并按结果保存或释放
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">后续动作委托</param>
    /// <returns>异步执行任务</returns>
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context
        , ActionExecutionDelegate next
    ) {
        var headerKey = context.HttpContext.Request.Headers[HEADER_NAME].ToString().Trim();
        switch (headerKey.Length) {
            case 0:
                _ = await next().ConfigureAwait(false);
                return;
            case > _MAX_KEY_LENGTH:
                context.Result = Reject(StatusCodes.Status400BadRequest, "Idempotency key is too long");
                return;
        }

        var service = context.HttpContext.RequestServices.GetRequiredService<IdempotencyService>();
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        var fingerprint = IdempotencyService.ComputeFingerprint(JsonSerializer.Serialize(context.ActionArguments, options));
        var key = BuildKey(context, headerKey);
        var begin = await TryBeginAsync(context, service, key, fingerprint).ConfigureAwait(false);
        if (begin is null) {
            _ = await next().ConfigureAwait(false);
            return;
        }

        if (begin.State != IdempotencyState.Started) {
            context.Result = BuildShortCircuitResult(context, begin);
            return;
        }

        var saved = false;
        try {
            var executed = await next().ConfigureAwait(false);
            saved = await TrySaveAsync(context, service, key, fingerprint, executed, options).ConfigureAwait(false);
        }
        finally {
            if (!saved) {
                await TryAbandonAsync(context, service, key).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     生成 Redis 键：按用户、接口和幂等键隔离，不同用户或接口可以使用相同的键
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="headerKey">请求头中的幂等键</param>
    /// <returns>Redis 键</returns>
    private static string BuildKey(
        ActionExecutingContext context
        , string headerKey
    ) {
        var user = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var template = context.ActionDescriptor is ControllerActionDescriptor descriptor
            ? descriptor.AttributeRouteInfo?.Template ?? descriptor.ActionName
            : context.ActionDescriptor.DisplayName;
        return $"{IdempotencyService.KEY_PREFIX}key:{user}:{template}:{headerKey}";
    }

    /// <summary>
    ///     把非首次请求的检查结果转换为直接返回的响应
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="begin">幂等检查结果</param>
    /// <returns>直接返回的响应</returns>
    private static IActionResult BuildShortCircuitResult(
        ActionExecutingContext context
        , IdempotencyBeginResult begin
    ) {
        switch (begin.State) {
            case IdempotencyState.Replay:
                context.HttpContext.Response.Headers[REPLAY_HEADER_NAME] = "true";
                return new ContentResult
                {
                    StatusCode = begin.Entry!.StatusCode, Content = begin.Entry.Body, ContentType = "application/json; charset=utf-8"
                };
            case IdempotencyState.Mismatch:
                return Reject(StatusCodes.Status422UnprocessableEntity, "Idempotency key was reused with a different request");
            default:
                // 使用 425 而不是 409，避免与业务中“资源已存在”的冲突含义混淆
                return Reject(_STATUS_TOO_EARLY, "Request is being processed");
        }
    }

    /// <summary>
    ///     记录幂等存储故障的警告日志
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="exception">异常</param>
    private static void LogStoreFailure(
        ActionExecutingContext context
        , Exception exception
    ) {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<IdempotentAttribute>>();
        _logStoreFailure(logger, exception);
    }

    /// <summary>
    ///     构造统一格式的拒绝响应
    /// </summary>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="message">英文消息</param>
    /// <returns>拒绝响应</returns>
    private static ObjectResult Reject(
        int statusCode
        , string message
    ) {
        return new ObjectResult(new ApiResponse<object>(statusCode, message, null)) { StatusCode = statusCode };
    }

    /// <summary>
    ///     尝试释放占位，存储故障时只记录警告，占位会在有效期后自动过期
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="service">幂等存储</param>
    /// <param name="key">Redis 键</param>
    /// <returns>异步释放任务</returns>
    private static async Task TryAbandonAsync(
        ActionExecutingContext context
        , IdempotencyService service
        , string key
    ) {
        try {
            await service.AbandonAsync(key).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            LogStoreFailure(context, exception);
        }
    }

    /// <summary>
    ///     尝试占位，存储故障时记录警告并返回空值，调用方据此放行请求
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="service">幂等存储</param>
    /// <param name="key">Redis 键</param>
    /// <param name="fingerprint">请求内容指纹</param>
    /// <returns>检查结果，存储故障时为空值</returns>
    private static async Task<IdempotencyBeginResult?> TryBeginAsync(
        ActionExecutingContext context
        , IdempotencyService service
        , string key
        , string fingerprint
    ) {
        try {
            return await service.BeginAsync(key, fingerprint, TimeSpan.FromSeconds(_LOCK_SECONDS)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            LogStoreFailure(context, exception);
            return null;
        }
    }

    /// <summary>
    ///     成功时保存响应，供相同的键回放；异常、非 2xx 或没有可序列化内容时不保存，存储故障时只记录警告
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="service">幂等存储</param>
    /// <param name="key">Redis 键</param>
    /// <param name="fingerprint">请求内容指纹</param>
    /// <param name="executed">动作执行结果</param>
    /// <param name="options">响应序列化选项</param>
    /// <returns>已保存响应时返回 true</returns>
    private async Task<bool> TrySaveAsync(
        ActionExecutingContext context
        , IdempotencyService service
        , string key
        , string fingerprint
        , ActionExecutedContext executed
        , JsonSerializerOptions options
    ) {
        if (executed.Exception is not null || executed.Result is not ObjectResult { Value: not null } result) {
            return false;
        }

        var statusCode = result.StatusCode ?? StatusCodes.Status200OK;
        if (statusCode is < 200 or >= 300) {
            return false;
        }

        try {
            var body = JsonSerializer.Serialize(result.Value, result.Value.GetType(), options);
            await service.CompleteAsync(key, fingerprint, statusCode, body, TimeSpan.FromSeconds(ReplaySeconds)).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            LogStoreFailure(context, exception);
            return false;
        }
    }
}