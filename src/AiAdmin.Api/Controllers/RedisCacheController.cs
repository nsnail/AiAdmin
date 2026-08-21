using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理 Redis 服务器和缓存键
/// </summary>
[ApiController]
[Authorize(Roles = "R_SUPER")]
[ApiDescription("Redis cache management")]
[Route("api/redis-cache")]
public sealed class RedisCacheController(IConnectionMultiplexer connectionMultiplexer) : ControllerBase
{
    private const int MaxScanCount = 200;
    private const int MaxValueLength = 1_000_000;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CpuSample> _cpuSamples = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     查询 Redis 服务器运行信息
    /// </summary>
    /// <returns>Redis 服务器信息</returns>
    [HttpGet("server-info")]
    [ApiDescription("Query Redis server information")]
    public async Task<ActionResult<ApiResponse<RedisServerInfoResult>>> ServerInfoAsync() {
        var server = GetServer();
        var database = connectionMultiplexer.GetDatabase();
        var endpoint = server.EndPoint.ToString() ?? string.Empty;
        var info = await TryGetServerInfoAsync(server).ConfigureAwait(false);
        var databaseSize = await TryGetDatabaseSizeAsync(server, database.Database).ConfigureAwait(false);
        var cpuSeconds = GetDoubleInfo(info, "used_cpu_sys") + GetDoubleInfo(info, "used_cpu_user");
        var cpuUsagePercent = CalculateCpuUsage(endpoint, cpuSeconds);
        var hits = GetDoubleInfo(info, "keyspace_hits");
        var misses = GetDoubleInfo(info, "keyspace_misses");
        var cacheHitRatePercent = hits + misses <= 0 ? 0 : hits / (hits + misses) * 100;
        var result = new RedisServerInfoResult(
            endpoint
            , GetInfo(info, "redis_version", "-" )
            , GetInfo(info, "redis_mode", "-" )
            , GetLongInfo(info, "connected_clients")
            , GetInfo(info, "used_memory_human", "-" )
            , GetInfo(info, "maxmemory_human", "-" )
            , databaseSize
            , cpuUsagePercent
            , (long)GetDoubleInfo(info, "uptime_in_seconds")
            , cacheHitRatePercent
        );
        return Ok(ApiResponse<RedisServerInfoResult>.Ok(result));
    }

    /// <summary>
    ///     扫描 Redis 缓存键
    /// </summary>
    /// <param name="pattern">键匹配模式</param>
    /// <param name="limit">最多返回数量</param>
    /// <returns>缓存键列表</returns>
    [HttpGet("keys")]
    [ApiDescription("Query Redis cache keys")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RedisCacheKeyResult>>>> KeysAsync(
        [FromQuery] string? pattern = null
        , [FromQuery] int limit = 100
    ) {
        var server = GetServer();
        var database = connectionMultiplexer.GetDatabase();
        var results = new List<RedisCacheKeyResult>();
        var normalizedLimit = Math.Clamp(limit, 1, MaxScanCount);
        foreach (var key in server.Keys(database.Database, string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim(), normalizedLimit)) {
            var type = await database.KeyTypeAsync(key).ConfigureAwait(false);
            var ttl = await database.KeyTimeToLiveAsync(key).ConfigureAwait(false);
            var memoryBytes = await TryGetMemoryBytesAsync(database, key).ConfigureAwait(false);
            var length = await GetLengthAsync(database, key, type).ConfigureAwait(false);
            results.Add(new RedisCacheKeyResult(key.ToString(), type.ToString(), ttl?.TotalMilliseconds is { } milliseconds ? (long)milliseconds : -1, memoryBytes, length));
            if (results.Count >= normalizedLimit) {
                break;
            }
        }

        return Ok(ApiResponse<IReadOnlyList<RedisCacheKeyResult>>.Ok(results));
    }

    /// <summary>
    ///     查询 Redis 缓存键内容
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>缓存键内容</returns>
    [HttpGet("value")]
    [ApiDescription("Query Redis cache value")]
    public async Task<ActionResult<ApiResponse<RedisCacheValueResult>>> ValueAsync([FromQuery] string key) {
        var database = connectionMultiplexer.GetDatabase();
        var redisKey = new RedisKey(key);
        if (!await database.KeyExistsAsync(redisKey).ConfigureAwait(false)) {
            return NotFound(new ApiResponse<object>(404, "Redis cache key not found", null));
        }

        var type = await database.KeyTypeAsync(redisKey).ConfigureAwait(false);
        var value = type == RedisType.String
            ? await database.StringGetAsync(redisKey).ConfigureAwait(false)
            : new RedisValue($"Redis type {type} is not editable by this client");
        var text = value.ToString();
        if (text.Length > MaxValueLength) {
            text = text[..MaxValueLength];
        }

        var ttl = await database.KeyTimeToLiveAsync(redisKey).ConfigureAwait(false);
        var memoryBytes = await TryGetMemoryBytesAsync(database, redisKey).ConfigureAwait(false);
        var length = await GetLengthAsync(database, redisKey, type).ConfigureAwait(false);
        return Ok(ApiResponse<RedisCacheValueResult>.Ok(new RedisCacheValueResult(
            key, type.ToString(), text, ttl?.TotalMilliseconds is { } milliseconds ? (long)milliseconds : -1, memoryBytes, length
        )));
    }

    /// <summary>
    ///     新增或更新 Redis 字符串缓存
    /// </summary>
    /// <param name="request">缓存保存请求</param>
    /// <returns>保存后的缓存内容</returns>
    [HttpPut("value")]
    [ApiDescription("Save Redis cache value")]
    public async Task<ActionResult<ApiResponse<RedisCacheValueResult>>> SaveAsync(SaveRedisCacheRequest request) {
        var key = request.Key.Trim();
        if (key.Length == 0) {
            return BadRequest(new ApiResponse<object>(400, "Redis cache key is required", null));
        }

        var database = connectionMultiplexer.GetDatabase();
        TimeSpan? expiry = request.ExpireSeconds > 0 ? TimeSpan.FromSeconds(request.ExpireSeconds) : null;
        _ = await database.StringSetAsync(key, request.Value, expiry).ConfigureAwait(false);
        var ttl = expiry?.TotalMilliseconds is { } milliseconds ? (long)milliseconds : -1;
        var memoryBytes = await TryGetMemoryBytesAsync(database, key).ConfigureAwait(false);
        return Ok(ApiResponse<RedisCacheValueResult>.Ok(new RedisCacheValueResult(key, nameof(RedisType.String), request.Value, ttl, memoryBytes, request.Value.Length), "Redis cache saved"));
    }

    /// <summary>
    ///     删除 Redis 缓存键
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("value")]
    [ApiDescription("Delete Redis cache value")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromQuery] string key) {
        var deleted = await connectionMultiplexer.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        return deleted
            ? Ok(ApiResponse<object>.Ok(new { }, "Redis cache deleted"))
            : NotFound(new ApiResponse<object>(404, "Redis cache key not found", null));
    }

    private static string GetInfo(IReadOnlyDictionary<string, string> info, string key, string fallback = "") {
        return info.TryGetValue(key, out var value) ? value : fallback;
    }

    private static long GetLongInfo(IReadOnlyDictionary<string, string> info, string key) {
        return long.TryParse(GetInfo(info, key), out var value) ? value : 0;
    }

    private static double GetDoubleInfo(IReadOnlyDictionary<string, string> info, string key) {
        return double.TryParse(GetInfo(info, key), out var value) ? value : 0;
    }

    private static double CalculateCpuUsage(string endpoint, double cpuSeconds) {
        var now = DateTimeOffset.UtcNow;
        var current = new CpuSample(cpuSeconds, now);
        if (!_cpuSamples.TryGetValue(endpoint, out var previous)) {
            _cpuSamples[endpoint] = current;
            return 0;
        }

        _cpuSamples[endpoint] = current;
        var elapsedSeconds = (now - previous.Timestamp).TotalSeconds;
        return elapsedSeconds <= 0 || cpuSeconds < previous.CpuSeconds
            ? 0
            : Math.Clamp((cpuSeconds - previous.CpuSeconds) / elapsedSeconds * 100, 0, 100);
    }

    /// <summary>
    ///     尝试读取 Redis INFO，未开启管理员权限时返回空信息
    /// </summary>
    /// <param name="server">Redis 服务器</param>
    /// <returns>Redis INFO 字段</returns>
    private static async Task<IReadOnlyDictionary<string, string>> TryGetServerInfoAsync(IServer server) {
        try {
            return (await server.InfoAsync().ConfigureAwait(false))
                .SelectMany(x => x)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (RedisCommandException) {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     尝试读取 Redis 数据库键数量
    /// </summary>
    /// <param name="server">Redis 服务器</param>
    /// <param name="database">数据库编号</param>
    /// <returns>键数量，无法读取时返回零</returns>
    private static async Task<long> TryGetDatabaseSizeAsync(IServer server, int database) {
        try {
            return await server.DatabaseSizeAsync(database).ConfigureAwait(false);
        }
        catch (RedisCommandException) {
            return 0;
        }
    }

    /// <summary>
    ///     尝试读取单个 Redis 键的内存占用
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <returns>占用字节数，不支持时返回零</returns>
    private static async Task<long> TryGetMemoryBytesAsync(IDatabase database, RedisKey key) {
        try {
            var result = await database.ExecuteAsync("MEMORY", "USAGE", key).ConfigureAwait(false);
            return long.TryParse(result.ToString(), out var bytes) ? bytes : 0;
        }
        catch (RedisCommandException) {
            return 0;
        }
    }

    private static async Task<long> GetLengthAsync(IDatabase database, RedisKey key, RedisType type) {
        return type switch {
            RedisType.String => await database.StringLengthAsync(key).ConfigureAwait(false),
            RedisType.List => await database.ListLengthAsync(key).ConfigureAwait(false),
            RedisType.Set => await database.SetLengthAsync(key).ConfigureAwait(false),
            RedisType.Hash => await database.HashLengthAsync(key).ConfigureAwait(false),
            RedisType.SortedSet => await database.SortedSetLengthAsync(key).ConfigureAwait(false),
            _ => 0
        };
    }

    private IServer GetServer() {
        var server = connectionMultiplexer.GetServers().FirstOrDefault(x => x.IsConnected && !x.IsReplica);
        return server ?? throw new InvalidOperationException("No connected Redis server is available");
    }

    private sealed record CpuSample(double CpuSeconds, DateTimeOffset Timestamp);
}