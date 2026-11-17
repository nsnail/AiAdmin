using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     按类型分页读取 Redis 集合类缓存键的元素，避免一次取回全部大容量数据
/// </summary>
public static class RedisItemReader
{
    /// <summary>
    ///     单个元素文本的长度上限，超出部分被截断
    /// </summary>
    public const int MAX_ITEM_LENGTH = 10_000;

    /// <summary>
    ///     每页元素数量的上限
    /// </summary>
    public const int MAX_PAGE_SIZE = 500;

    /// <summary>
    ///     读取指定类型的一页元素
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="type">键的数据类型</param>
    /// <param name="cursor">游标，为空表示从头开始，含义见 <see cref="RedisCacheItemsResult" /> 的 NextCursor</param>
    /// <param name="pageSize">每页元素数量，会被限制在 1 到 <see cref="MAX_PAGE_SIZE" /></param>
    /// <returns>一页元素；类型不是受支持的集合类型时返回空</returns>
    public static async Task<RedisCacheItemsResult?> ReadAsync(
        IDatabase database
        , RedisKey key
        , RedisType type
        , string? cursor
        , int pageSize
    ) {
        pageSize = Math.Clamp(pageSize, 1, MAX_PAGE_SIZE);
        return type switch
        {
            RedisType.List => await ReadListAsync(database, key, cursor, pageSize).ConfigureAwait(false)
            , RedisType.SortedSet => await ReadSortedSetAsync(database, key, cursor, pageSize).ConfigureAwait(false)
            , RedisType.Hash => await ReadHashAsync(database, key, cursor, pageSize).ConfigureAwait(false)
            , RedisType.Set => await ReadSetAsync(database, key, cursor, pageSize).ConfigureAwait(false)
            , RedisType.Stream => await ReadStreamAsync(database, key, cursor, pageSize).ConfigureAwait(false)
            , _ => null
        };
    }

    /// <summary>
    ///     把 Redis 回复转换为数组，回复不是数组时返回空数组
    /// </summary>
    /// <param name="result">Redis 回复</param>
    /// <returns>回复中的元素数组</returns>
    private static RedisResult[] AsArray(RedisResult? result) {
        return (RedisResult[]?)result ?? [];
    }

    /// <summary>
    ///     截断过长的元素文本
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="truncated">发生截断时置为真</param>
    /// <returns>不超过上限的文本</returns>
    private static string Clip(
        string text
        , ref bool truncated
    ) {
        if (text.Length <= MAX_ITEM_LENGTH) {
            return text;
        }

        truncated = true;
        return text[..MAX_ITEM_LENGTH];
    }

    /// <summary>
    ///     把游标解析为非负整数起点，无法解析时从头开始
    /// </summary>
    /// <param name="cursor">游标文本</param>
    /// <returns>起始位置</returns>
    private static long ParseOffset(string? cursor) {
        return long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) ? offset : 0;
    }

    /// <summary>
    ///     用 HSCAN 游标读取 Hash，不会阻塞服务器
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">SCAN 游标，为空表示从头开始</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>一页元素</returns>
    private static async Task<RedisCacheItemsResult> ReadHashAsync(
        IDatabase database
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var total = await database.HashLengthAsync(key).ConfigureAwait(false);
        var (entries, next) = await ScanAsync(database, "HSCAN", key, cursor, pageSize).ConfigureAwait(false);
        var truncated = false;
        var items = new List<RedisCacheItemResult>(entries.Count / 2);
        for (var i = 0; i + 1 < entries.Count; i += 2) {
            items.Add(new RedisCacheItemResult(items.Count, entries[i], Clip(entries[i + 1], ref truncated), null));
        }

        return new RedisCacheItemsResult(key.ToString(), nameof(RedisType.Hash), items, total, next, truncated);
    }

    /// <summary>
    ///     按下标范围读取 List
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">起始下标</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>一页元素</returns>
    private static async Task<RedisCacheItemsResult> ReadListAsync(
        IDatabase database
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var total = await database.ListLengthAsync(key).ConfigureAwait(false);
        var start = ParseOffset(cursor);
        var values = await database.ListRangeAsync(key, start, start + pageSize - 1).ConfigureAwait(false);
        var truncated = false;
        List<RedisCacheItemResult> items =
        [
            with(values.Length), .. values.Select((
                    t
                    , i
                ) => new RedisCacheItemResult(start + i, null, Clip(t.ToString(), ref truncated), null)
            )
        ];

        var next = start + values.Length;
        return new RedisCacheItemsResult(
            key.ToString(), nameof(RedisType.List), items, total, next < total ? next.ToString(CultureInfo.InvariantCulture) : null, truncated
        );
    }

    /// <summary>
    ///     用 SSCAN 游标读取 Set，不会阻塞服务器
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">SCAN 游标，为空表示从头开始</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>一页元素</returns>
    private static async Task<RedisCacheItemsResult> ReadSetAsync(
        IDatabase database
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var total = await database.SetLengthAsync(key).ConfigureAwait(false);
        var (entries, next) = await ScanAsync(database, "SSCAN", key, cursor, pageSize).ConfigureAwait(false);
        var truncated = false;
        List<RedisCacheItemResult> items =
        [
            with(entries.Count), .. entries.Select((
                    t
                    , i
                ) => new RedisCacheItemResult(i, null, Clip(t, ref truncated), null)
            )
        ];

        return new RedisCacheItemsResult(key.ToString(), nameof(RedisType.Set), items, total, next, truncated);
    }

    /// <summary>
    ///     按排名范围读取 SortedSet，分数从小到大
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">起始排名</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>一页元素</returns>
    private static async Task<RedisCacheItemsResult> ReadSortedSetAsync(
        IDatabase database
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var total = await database.SortedSetLengthAsync(key).ConfigureAwait(false);
        var start = ParseOffset(cursor);
        var entries = await database.SortedSetRangeByRankWithScoresAsync(key, start, start + pageSize - 1).ConfigureAwait(false);
        var truncated = false;
        List<RedisCacheItemResult> items =
        [
            with(entries.Length), .. entries.Select((
                    t
                    , i
                ) => new RedisCacheItemResult(start + i, null, Clip(t.Element.ToString(), ref truncated), t.Score)
            )
        ];

        var next = start + entries.Length;
        return new RedisCacheItemsResult(
            key.ToString(), nameof(RedisType.SortedSet), items, total, next < total ? next.ToString(CultureInfo.InvariantCulture) : null, truncated
        );
    }

    /// <summary>
    ///     按条目 ID 顺序读取 Stream，每个条目的字段序列化为 JSON
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">上一页最后一个条目 ID（不含），为空表示从头开始</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>一页元素</returns>
    private static async Task<RedisCacheItemsResult> ReadStreamAsync(
        IDatabase database
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var total = await database.StreamLengthAsync(key).ConfigureAwait(false);
        var after = string.IsNullOrEmpty(cursor) ? "-" : $"({cursor}";
        var reply = await database.ExecuteAsync("XRANGE", key, after, "+", "COUNT", pageSize).ConfigureAwait(false);
        var truncated = false;
        var items = new List<RedisCacheItemResult>();
        string? lastId = null;
        foreach (var entry in AsArray(reply)) {
            var parts = AsArray(entry);
            lastId = parts[0].ToString();
            var fields = AsArray(parts[1]);
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i + 1 < fields.Length; i += 2) {
                map[fields[i].ToString()] = fields[i + 1].ToString();
            }

            items.Add(new RedisCacheItemResult(items.Count, lastId, Clip(JsonSerializer.Serialize(map), ref truncated), null));
        }

        return new RedisCacheItemsResult(key.ToString(), nameof(RedisType.Stream), items, total, items.Count == pageSize ? lastId : null, truncated);
    }

    /// <summary>
    ///     执行一次 HSCAN 或 SSCAN，返回本次扫描到的内容和下一个游标。SCAN 的 COUNT 只是提示，单次返回数量可能多于或少于页大小
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="command">HSCAN 或 SSCAN</param>
    /// <param name="key">缓存键</param>
    /// <param name="cursor">SCAN 游标，为空表示从头开始</param>
    /// <param name="pageSize">COUNT 提示值</param>
    /// <returns>本次扫描到的文本列表（Hash 为字段与值交替），以及下一个游标，扫描完成时游标为空</returns>
    private static async Task<(List<string> Entries, string? NextCursor)> ScanAsync(
        IDatabase database
        , string command
        , RedisKey key
        , string? cursor
        , int pageSize
    ) {
        var start = string.IsNullOrEmpty(cursor) || !cursor.All(char.IsAsciiDigit) ? "0" : cursor;
        var reply = await database.ExecuteAsync(command, key, start, "COUNT", pageSize).ConfigureAwait(false);
        var parts = AsArray(reply);
        var nextCursor = parts[0].ToString();
        var entries = AsArray(parts[1]).Select(x => x.ToString()).ToList();
        return (entries, nextCursor is null or "0" ? null : nextCursor);
    }
}