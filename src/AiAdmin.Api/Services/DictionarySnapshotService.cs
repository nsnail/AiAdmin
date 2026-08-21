using System.Text.Json;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     将内置字典目录保存到 Redis，并在缓存不可用时回退数据库
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="logger">日志记录器</param>
public sealed class DictionarySnapshotService(
    AppDbContext db
    , IConnectionMultiplexer connectionMultiplexer
    , ILogger<DictionarySnapshotService> logger)
{
    /// <summary>
    ///     计划作业占位符目录编码
    /// </summary>
    public const string SCHEDULED_JOB_PLACEHOLDERS_CODE = "scheduled_job_placeholders";

    /// <summary>
    ///     系统设置目录编码
    /// </summary>
    public const string SYSTEM_SETTINGS_CODE = "system_settings";

    private const string _KEY_PREFIX = RedisKeyPrefix.VALUE + "dictionary:snapshot:";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, string, Exception?> _logRedisReadFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(3201, "DictionarySnapshotReadFailed"), "Unable to read dictionary snapshot {CategoryCode} from Redis"
    );

    private static readonly Action<ILogger, string, Exception?> _logRedisWriteFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(3202, "DictionarySnapshotWriteFailed"), "Unable to write dictionary snapshot {CategoryCode} to Redis"
    );

    /// <summary>
    ///     获取内置目录的字典项快照，Redis 未命中时从数据库加载并回写
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按排序值和主键排序的字典项快照</returns>
    /// <exception cref="ArgumentOutOfRangeException">目录不属于支持快照的内置目录时引发</exception>
    public async Task<IReadOnlyList<DictionarySnapshotItem>> GetItemsAsync(
        string categoryCode
        , CancellationToken cancellationToken = default
    ) {
        EnsureSupported(categoryCode);
        try {
            var value = await connectionMultiplexer.GetDatabase().StringGetAsync(GetKey(categoryCode)).ConfigureAwait(false);
            if (value.HasValue) {
                var items = JsonSerializer.Deserialize<List<DictionarySnapshotItem>>(value.ToString(), _jsonOptions);
                if (items is not null) {
                    return items;
                }
            }
        }
        catch (Exception exception) when (exception is RedisException or JsonException) {
            _logRedisReadFailed(logger, categoryCode, exception);
        }

        var databaseItems = await LoadFromDatabaseAsync(categoryCode, cancellationToken).ConfigureAwait(false);
        await WriteAsync(categoryCode, databaseItems).ConfigureAwait(false);
        return databaseItems;
    }

    /// <summary>
    ///     从数据库重新生成指定内置目录的 Redis 快照
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步执行任务</returns>
    public async Task RefreshAsync(
        string categoryCode
        , CancellationToken cancellationToken = default
    ) {
        if (!IsSupported(categoryCode)) {
            return;
        }

        var items = await LoadFromDatabaseAsync(categoryCode, cancellationToken).ConfigureAwait(false);
        await WriteAsync(categoryCode, items).ConfigureAwait(false);
    }

    /// <summary>
    ///     删除指定内置目录的 Redis 快照
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <returns>异步执行任务</returns>
    public async Task RemoveAsync(string categoryCode) {
        if (!IsSupported(categoryCode)) {
            return;
        }

        try {
            _ = await connectionMultiplexer.GetDatabase().KeyDeleteAsync(GetKey(categoryCode)).ConfigureAwait(false);
        }
        catch (RedisException exception) {
            _logRedisWriteFailed(logger, categoryCode, exception);
        }
    }

    /// <summary>
    ///     校验目录是否支持 Redis 快照
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <exception cref="ArgumentOutOfRangeException">目录不属于支持快照的内置目录时引发</exception>
    private static void EnsureSupported(string categoryCode) {
        if (!IsSupported(categoryCode)) {
            throw new ArgumentOutOfRangeException(nameof(categoryCode), categoryCode, "Dictionary category does not support Redis snapshots");
        }
    }

    /// <summary>
    ///     获取指定目录的 Redis 键
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <returns>Redis 键</returns>
    private static RedisKey GetKey(string categoryCode) {
        return _KEY_PREFIX + categoryCode;
    }

    /// <summary>
    ///     判断目录是否为需要保存快照的内置目录
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <returns>需要保存快照时返回 true</returns>
    private static bool IsSupported(string categoryCode) {
        return categoryCode is SYSTEM_SETTINGS_CODE or SCHEDULED_JOB_PLACEHOLDERS_CODE;
    }

    /// <summary>
    ///     从数据库读取指定目录的完整字典项
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>字典项快照</returns>
    private Task<List<DictionarySnapshotItem>> LoadFromDatabaseAsync(
        string categoryCode
        , CancellationToken cancellationToken
    ) {
        return db
            .DictionaryItems.AsNoTracking()
            .Where(x => x.Category.Code == categoryCode)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(x => new DictionarySnapshotItem
                {
                    Label = x.Label
                    , Value = x.Value
                    , IsEnabled = x.IsEnabled
                    , Sort = x.Sort
                    , Remark = x.Remark
                }
            )
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     将字典项写入 Redis
    /// </summary>
    /// <param name="categoryCode">字典目录编码</param>
    /// <param name="items">字典项快照</param>
    /// <returns>异步执行任务</returns>
    private async Task WriteAsync(
        string categoryCode
        , IReadOnlyList<DictionarySnapshotItem> items
    ) {
        try {
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            _ = await connectionMultiplexer.GetDatabase().StringSetAsync(GetKey(categoryCode), json).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or JsonException) {
            _logRedisWriteFailed(logger, categoryCode, exception);
        }
    }
}