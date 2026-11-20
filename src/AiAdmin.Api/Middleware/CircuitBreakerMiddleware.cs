using System.Diagnostics;
using System.Globalization;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     按接口配置的熔断规则保护故障接口：窗口内失败率达到阈值时暂停放行，到期后以少量试探请求探测恢复。
///     熔断时返回 429 而不是 502/503，因为网关对 502/503 会换节点重试并把节点记为失败，多个实例同时熔断会导致全部节点被摘除；
///     熔断自身与限流返回的 429 不计入失败统计，避免熔断放大自己；熔断组件故障时放行，避免 Redis 异常拖垮全部接口
/// </summary>
/// <param name="next">后续请求处理委托</param>
/// <param name="logger">日志记录器</param>
public sealed class CircuitBreakerMiddleware(RequestDelegate next, ILogger<CircuitBreakerMiddleware> logger)
{
    /// <summary>
    ///     熔断器故障日志委托
    /// </summary>
    private static readonly Action<ILogger, Exception?> _logBreakerFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(2, nameof(_logBreakerFailure)), "Circuit breaker check failed, request is allowed"
    );

    /// <summary>
    ///     判断一次请求是否算作失败：抛出异常、5xx 响应，或耗时超过慢调用阈值；
    ///     429 是限流或熔断自己的响应，既不算失败也不算成功，调用方应跳过上报
    /// </summary>
    /// <param name="statusCode">响应状态码</param>
    /// <param name="threw">请求处理是否抛出了异常</param>
    /// <param name="elapsedMs">请求耗时毫秒数</param>
    /// <param name="slowCallMs">慢调用阈值，0 表示不统计慢调用</param>
    /// <returns>请求算作失败返回 true</returns>
    public static bool IsFailure(
        int statusCode
        , bool threw
        , long elapsedMs
        , int slowCallMs
    ) {
        return threw || statusCode >= 500 || (slowCallMs > 0 && elapsedMs > slowCallMs);
    }

    /// <summary>
    ///     对命中熔断规则的请求做放行判定，放行后执行请求并上报结果
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="ruleCache">熔断规则缓存</param>
    /// <param name="breaker">熔断器</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , CircuitBreakerRuleCache ruleCache
        , CircuitBreaker breaker
    ) {
        var template = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.AttributeRouteInfo?.Template;
        if (template is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var (rule, permit) = await TryAcquireAsync(context, template, ruleCache, breaker).ConfigureAwait(false);
        if (rule is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!permit!.Allowed) {
            await RejectAsync(context, rule, permit).ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var threw = false;
        try {
            await next(context).ConfigureAwait(false);
        }
        catch {
            threw = true;
            throw;
        }
        finally {
            await ReportAsync(context, rule, permit, threw, stopwatch.ElapsedMilliseconds, breaker).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     返回 429 并带上熔断状态，不使用 502/503，避免触发网关的故障转移和节点摘除
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="rule">命中的熔断规则</param>
    /// <param name="permit">放行判定</param>
    /// <returns>异步任务</returns>
    private static Task RejectAsync(
        HttpContext context
        , CircuitBreakerRuleSnapshot rule
        , CircuitBreakerPermit permit
    ) {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = permit.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-Circuit-State"] = permit.State == CircuitBreakerState.Open ? "open" : "half-open";
        context.Response.Headers["X-Circuit-Rule"] = rule.Id.ToString(CultureInfo.InvariantCulture);
        return context.Response.WriteAsJsonAsync(
            new ApiResponse<CircuitBreakerRejection>(
                429, "API is temporarily unavailable, retry later"
                , new CircuitBreakerRejection(rule.Id.ToString(CultureInfo.InvariantCulture), permit.State, permit.RetryAfterSeconds)
            ), context.RequestAborted
        );
    }

    /// <summary>
    ///     请求结束后上报结果，上报失败只记日志，不影响已经产生的响应
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="rule">命中的熔断规则</param>
    /// <param name="permit">放行判定</param>
    /// <param name="threw">请求处理是否抛出了异常</param>
    /// <param name="elapsedMs">请求耗时毫秒数</param>
    /// <param name="breaker">熔断器</param>
    /// <returns>异步任务</returns>
    private async Task ReportAsync(
        HttpContext context
        , CircuitBreakerRuleSnapshot rule
        , CircuitBreakerPermit permit
        , bool threw
        , long elapsedMs
        , CircuitBreaker breaker
    ) {
        // 下游限流返回的 429 不是接口自身的结果，跳过上报；试探请求被限流时也不占用试探配额的成败判定
        if (!threw && context.Response.StatusCode == StatusCodes.Status429TooManyRequests) {
            return;
        }

        try {
            _ = await breaker
                .ReportAsync(rule, IsFailure(context.Response.StatusCode, threw, elapsedMs, rule.SlowCallMs), permit.Probe)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            _logBreakerFailure(logger, ex);
        }
    }

    /// <summary>
    ///     查找接口的熔断规则并判定是否放行，没有规则或熔断组件故障时视为放行且不统计
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="template">接口路由模板</param>
    /// <param name="ruleCache">熔断规则缓存</param>
    /// <param name="breaker">熔断器</param>
    /// <returns>命中的规则和放行判定，没有规则或故障时规则为空值</returns>
    private async Task<(CircuitBreakerRuleSnapshot? Rule, CircuitBreakerPermit? Permit)> TryAcquireAsync(
        HttpContext context
        , string template
        , CircuitBreakerRuleCache ruleCache
        , CircuitBreaker breaker
    ) {
        try {
            var rules = await ruleCache.GetAsync(context.RequestAborted).ConfigureAwait(false);
            return !rules.TryGetValue(ApiEndpointKey.Create(context.Request.Method, template), out var rule)
                ? (null, null)
                : (rule, await breaker.TryAcquireAsync(rule).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            _logBreakerFailure(logger, ex);
            return (null, null);
        }
    }
}