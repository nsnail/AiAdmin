using System.Text.Json;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AiAdmin.Api.Services;

/// <summary>
///     缓存已启用的接口限流规则，按接口键分组，规则变化后淘汰缓存，各实例下次请求时重新加载
/// </summary>
/// <param name="cache">分布式缓存</param>
/// <param name="scopeFactory">服务作用域工厂</param>
/// <param name="logger">日志记录器</param>
public sealed class RateLimitRuleCache(IDistributedCache cache, IServiceScopeFactory scopeFactory, ILogger<RateLimitRuleCache> logger)
{
    private const string _CACHE_KEY = "rate-limit-rules";
    private static readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>
    ///     规则加载失败的日志委托
    /// </summary>
    private static readonly Action<ILogger, Exception?> _logLoadFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(3, nameof(_logLoadFailure)), "Failed to load rate limit rules, requests are allowed until the next retry"
    );

    /// <summary>
    ///     加载失败后的退避，期间不再尝试加载，避免每个请求都去访问故障的数据库或 Redis
    /// </summary>
    private readonly RuleLoadBackoff _backoff = new(TimeSpan.FromSeconds(5));

    /// <summary>
    ///     读取全部已启用的限流规则，缓存未命中时从数据库加载
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>接口键到规则集合的字典</returns>
    public async Task<IReadOnlyDictionary<string, RateLimitRuleSnapshot[]>> GetAsync(CancellationToken cancellationToken = default) {
        // 加载失败后的退避期内直接按没有规则处理：不读 Redis、不查库，避免每个请求都重复一次必然失败的访问
        if (_backoff.IsBackingOff) {
            return new Dictionary<string, RateLimitRuleSnapshot[]>();
        }

        try {
            return await ReadAsync(cancellationToken).ConfigureAwait(false) ?? await LoadAndStoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            // 数据库或 Redis 故障、表尚未创建等都会走到这里：每个退避周期只记一条日志，并按没有规则放行
            if (_backoff.Fail()) {
                _logLoadFailure(logger, ex);
            }

            return new Dictionary<string, RateLimitRuleSnapshot[]>();
        }
    }

    /// <summary>
    ///     使规则缓存失效，规则新增、修改、删除或接口同步后调用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task InvalidateAsync(CancellationToken cancellationToken = default) {
        return cache.RemoveAsync(_CACHE_KEY, cancellationToken);
    }

    /// <summary>
    ///     在加载锁内再次确认缓存，仍未命中时从数据库加载并写入缓存，加载成功后结束退避
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>接口键到规则的字典</returns>
    private async Task<IReadOnlyDictionary<string, RateLimitRuleSnapshot[]>> LoadAndStoreAsync(CancellationToken cancellationToken) {
        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var cached = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null) {
                return cached;
            }

            var rules = await LoadAsync(cancellationToken).ConfigureAwait(false);
            await cache
                .SetStringAsync(
                    _CACHE_KEY, JsonSerializer.Serialize(rules)
                    , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) }, cancellationToken
                )
                .ConfigureAwait(false);
            _backoff.Succeed();
            return rules;
        }
        finally {
            _ = _loadLock.Release();
        }
    }

    /// <summary>
    ///     从数据库加载已启用的规则并按接口键分组
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>接口键到规则集合的字典</returns>
    private async Task<Dictionary<string, RateLimitRuleSnapshot[]>> LoadAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db
            .RateLimitRules.AsNoTracking()
            .Where(x => x.IsEnabled)
            .Select(x => new
                {
                    x.ApiEndpoint.Method
                    , x.ApiEndpoint.Path
                    , Rule = new RateLimitRuleSnapshot(x.Id, x.Algorithm, x.Scope, x.RoleCode, x.UserId, x.Limit, x.WindowSeconds)
                }
            )
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .GroupBy(x => ApiEndpointKey.Create(x.Method, x.Path), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Rule).ToArray(), StringComparer.Ordinal);
    }

    /// <summary>
    ///     读取缓存中的规则，未命中时返回空值
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则字典，缓存不存在时为空值</returns>
    private async Task<Dictionary<string, RateLimitRuleSnapshot[]>?> ReadAsync(CancellationToken cancellationToken) {
        var json = await cache.GetStringAsync(_CACHE_KEY, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, RateLimitRuleSnapshot[]>>(json, JsonParsing.Options);
    }
}