using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Caching;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     缓存用户数据权限快照并通过版本号实现统一失效
/// </summary>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
public sealed class DataScopeCache(IConnectionMultiplexer connectionMultiplexer)
{
    private const string _KEY_PREFIX = RedisKeyPrefix.VALUE + "data-scope:snapshot:";
    private const string _VERSION_KEY = RedisKeyPrefix.VALUE + "data-scope:version";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     读取指定用户的数据权限快照
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存快照，不存在时返回空值</returns>
    public async Task<DataScopeSnapshot?> GetAsync(
        long userId
        , CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var database = connectionMultiplexer.GetDatabase();
        var version = await GetVersionAsync(database).ConfigureAwait(false);
        var value = await database.StringGetAsync(GetKey(version, userId)).ConfigureAwait(false);
        return value.HasValue ? JsonSerializer.Deserialize<DataScopeSnapshot>(value.ToString(), _jsonOptions) : null;
    }

    /// <summary>
    ///     递增数据权限缓存版本
    /// </summary>
    /// <returns>失效任务</returns>
    public Task InvalidateAsync() {
        return connectionMultiplexer.GetDatabase().StringIncrementAsync(_VERSION_KEY);
    }

    /// <summary>
    ///     保存指定用户的数据权限快照
    /// </summary>
    /// <param name="snapshot">数据权限快照</param>
    /// <returns>保存任务</returns>
    public async Task SetAsync(DataScopeSnapshot snapshot) {
        var database = connectionMultiplexer.GetDatabase();
        var version = await GetVersionAsync(database).ConfigureAwait(false);
        _ = await database
            .StringSetAsync(GetKey(version, snapshot.UserId), JsonSerializer.Serialize(snapshot, _jsonOptions), _lifetime)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     获取用户快照缓存键
    /// </summary>
    /// <param name="version">缓存版本</param>
    /// <param name="userId">用户主键</param>
    /// <returns>缓存键</returns>
    private static RedisKey GetKey(
        long version
        , long userId
    ) {
        return $"{_KEY_PREFIX}{version}:{userId}";
    }

    /// <summary>
    ///     获取当前缓存版本
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <returns>缓存版本</returns>
    private static async Task<long> GetVersionAsync(IDatabase database) {
        var version = await database.StringGetAsync(_VERSION_KEY).ConfigureAwait(false);
        if (version.HasValue && long.TryParse(version.ToString(), out var value)) {
            return value;
        }

        var initialized = await database.StringSetAsync(_VERSION_KEY, 1, null, When.NotExists).ConfigureAwait(false);
        return initialized
            ? 1
            : long.Parse((await database.StringGetAsync(_VERSION_KEY).ConfigureAwait(false)).ToString(), CultureInfo.InvariantCulture);
    }
}