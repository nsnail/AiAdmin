using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     按接口配置的限流规则拦截请求，同一接口的多条规则必须全部通过；限流组件自身故障时放行，避免 Redis 异常拖垮全部接口
/// </summary>
/// <param name="next">后续请求处理委托</param>
/// <param name="logger">日志记录器</param>
public sealed class RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
{
    private const string _GLOBAL_SUBJECT = "global";

    /// <summary>
    ///     限流检查失败时的日志委托
    /// </summary>
    private static readonly Action<ILogger, Exception?> _logCheckFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(1, nameof(_logCheckFailure)), "Rate limit check failed, request is allowed"
    );

    /// <summary>
    ///     对命中限流规则的请求做判定，超限时返回 429 并带上 Retry-After 响应头
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="ruleCache">限流规则缓存</param>
    /// <param name="limiter">限流器</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , RateLimitRuleCache ruleCache
        , RateLimiter limiter
    ) {
        var template = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.AttributeRouteInfo?.Template;
        if (template is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        var rejected = await FindRejectionAsync(context, template, ruleCache, limiter).ConfigureAwait(false);
        if (rejected is null) {
            await next(context).ConfigureAwait(false);
            return;
        }

        // 响应头和响应体都带上触发的规则，便于客户端和排查时知道是哪条规则限流
        var rule = rejected.Rule!;
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = rejected.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Rule"] = rule.Id.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Limit"] = rule.Limit.ToString(CultureInfo.InvariantCulture);
        await context
            .Response.WriteAsJsonAsync(
                new ApiResponse<RateLimitRejection>(
                    429, "Too many requests, retry later"
                    , new RateLimitRejection(
                        rule.Id.ToString(CultureInfo.InvariantCulture), rule.Algorithm, rule.Scope, rule.RoleCode, rule.Limit, rule.WindowSeconds
                        , rejected.RetryAfterSeconds
                    )
                ), context.RequestAborted
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     解析规则对当前请求的限流主体，规则不适用于当前用户时返回空值
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="rule">限流规则</param>
    /// <returns>限流主体标识，规则不适用时为空值</returns>
    private static string ResolveSubject(
        HttpContext context
        , RateLimitRuleSnapshot rule
    ) {
        if (rule.Scope == RateLimitScope.Global) {
            return _GLOBAL_SUBJECT;
        }

        // 用户、角色和默认规则都按用户分别计数，是否适用已由规则选择器判定
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId)) {
            return $"user:{userId}";
        }

        // 未登录的请求没有用户身份，按客户端地址计数
        var address = context.Connection.RemoteIpAddress;
        return $"ip:{(address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address)}";
    }

    /// <summary>
    ///     依次判定接口的限流规则，返回第一条拒绝结果，全部通过返回空值
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="template">接口路由模板</param>
    /// <param name="ruleCache">限流规则缓存</param>
    /// <param name="limiter">限流器</param>
    /// <returns>被拒绝时的判定结果，放行时为空值</returns>
    private async Task<RateLimitDecision?> FindRejectionAsync(
        HttpContext context
        , string template
        , RateLimitRuleCache ruleCache
        , RateLimiter limiter
    ) {
        try {
            var rules = await ruleCache.GetAsync(context.RequestAborted).ConfigureAwait(false);
            if (!rules.TryGetValue(ApiEndpointKey.Create(context.Request.Method, template), out var matched)) {
                return null;
            }

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleCodes = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
            foreach (var rule in RateLimitRuleSelector.Select(matched, userId, roleCodes)) {
                var subject = ResolveSubject(context, rule);

                var decision = await limiter.CheckAsync(rule, subject).ConfigureAwait(false);
                if (!decision.Allowed) {
                    return decision;
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            _logCheckFailure(logger, ex);
            return null;
        }
    }
}