using System.Text.Json;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AiAdmin.Api.Services;

/// <summary>
///     缓存接口匿名配置和角色接口授权关系
/// </summary>
public sealed class ApiPermissionCache(IDistributedCache cache, IServiceScopeFactory scopeFactory)
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
            var cached = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null) {
                return cached;
            }

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                cached = await ReadAsync(cancellationToken).ConfigureAwait(false);
                if (cached is not null) {
                    return cached;
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
                await cache
                    .SetStringAsync(
                        _CACHE_KEY
                        , JsonSerializer.Serialize(
                            new PermissionCacheModel(
                                snapshot.HasApis, [.. snapshot.AnonymousKeys]
                                , snapshot.ByRole.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal)
                            )
                        ), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) }, cancellationToken
                    )
                    .ConfigureAwait(false);
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
        _ = cache.RemoveAsync(_CACHE_KEY);
    }

    private async Task<ApiPermissionSnapshot?> ReadAsync(CancellationToken cancellationToken) {
        var json = await cache.GetStringAsync(_CACHE_KEY, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }

        var model = JsonSerializer.Deserialize<PermissionCacheModel>(json);
        return model is null
            ? null
            : new ApiPermissionSnapshot(
                model.HasApis, model.AnonymousKeys.ToHashSet(StringComparer.Ordinal)
                , model.ByRole.ToDictionary(x => x.Key, x => x.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal)
            );
    }
}