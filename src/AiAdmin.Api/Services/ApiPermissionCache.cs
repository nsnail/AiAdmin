using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

// 缓存接口匿名配置和角色接口权限，避免每次请求访问数据库。
namespace AiAdmin.Api.Services;

/// <summary>
///     缓存接口匿名配置和角色接口授权关系
/// </summary>
public sealed class ApiPermissionCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
{
    private const string _CACHE_KEY = "api-permission-snapshot";
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);
    private long _version;

    /// <summary>
    ///     获取当前权限快照
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限快照</returns>
    public async Task<ApiPermissionSnapshot> GetAsync(CancellationToken cancellationToken = default) {
        // 使用双重检查和版本号，保证并发加载时只发布最新权限快照。
        while (true) {
            if (cache.TryGetValue<ApiPermissionSnapshot>(_CACHE_KEY, out var cached)) {
                return cached!;
            }

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                if (cache.TryGetValue(_CACHE_KEY, out cached)) {
                    return cached!;
                }

                var loadingVersion = Volatile.Read(ref _version);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasApis = await db.ApiEndpoints.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false);
                var anonymousKeys = await db
                    .ApiEndpoints.AsNoTracking()
                    .Where(x => x.AllowAnonymous)
                    .Select(x => new { x.Method, x.Path })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var rows = await db
                    .RoleApis.AsNoTracking()
                    .Where(x => x.Role.IsEnabled)
                    .Select(x => new { x.Role.Code, x.ApiEndpoint.Method, x.ApiEndpoint.Path })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var byRole = rows
                    .GroupBy(x => x.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key, group => group.Select(x => ApiEndpointKey.Create(x.Method, x.Path)).ToHashSet(StringComparer.Ordinal)
                        , StringComparer.Ordinal
                    );
                if (loadingVersion != Volatile.Read(ref _version)) {
                    continue;
                }

                var snapshot = new ApiPermissionSnapshot(
                    hasApis, anonymousKeys.Select(x => ApiEndpointKey.Create(x.Method, x.Path)).ToHashSet(StringComparer.Ordinal), byRole
                );
                _ = cache.Set(_CACHE_KEY, snapshot, TimeSpan.FromMinutes(30));
                return snapshot;
            }
            finally {
                _ = _cacheLock.Release();
            }
        }
    }

    /// <summary>
    ///     使权限缓存失效
    /// </summary>
    public void Invalidate() {
        // 权限或接口配置变化后立即淘汰旧快照。
        _ = Interlocked.Increment(ref _version);
        cache.Remove(_CACHE_KEY);
    }
}

/// <summary>
///     内存中的接口权限快照
/// </summary>
public sealed record ApiPermissionSnapshot(bool HasApis, IReadOnlySet<string> AnonymousKeys, IReadOnlyDictionary<string, HashSet<string>> ByRole)
{
    /// <summary>
    ///     判断多个角色对接口的权限并集是否包含目标接口
    /// </summary>
    /// <param name="roles">当前用户角色编码</param>
    /// <param name="apiKey">接口键</param>
    /// <returns>拥有权限时返回 true</returns>
    public bool Allows(
        IEnumerable<string> roles
        , string apiKey
    ) {
        return roles.Any(role => ByRole.TryGetValue(role, out var apiKeys) && apiKeys.Contains(apiKey));
    }
}