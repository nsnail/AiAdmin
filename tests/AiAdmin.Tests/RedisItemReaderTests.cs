using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证 Redis 集合类缓存键的分页读取：List、SortedSet、Hash、Set、Stream 在大容量下按页取回、游标可继续、不重复不遗漏。
///     需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:16399），未设置时每个测试直接返回，不执行任何断言
/// </summary>
public sealed class RedisItemReaderTests : IAsyncLifetime
{
    private const int _PAGE = 100;
    private const int _TOTAL = 1250;
    private IDatabase _database = null!;
    private string _prefix = string.Empty;
    private ConnectionMultiplexer? _redis;

    /// <summary>
    ///     清理测试写入的键并断开连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_redis is null) {
            return;
        }

        foreach (var name in new[] { "list", "zset", "hash", "set", "stream", "string" }) {
            _ = await _database.KeyDeleteAsync(_prefix + name).ConfigureAwait(false);
        }

        await _redis.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     验证 Hash 用 HSCAN 分页：所有字段都取到、字段与值对应、没有重复，SCAN 游标走完后下一页游标为空
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Hash_ScansAllFieldsOnceAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "hash";
        HashEntry[] entries = [.. Enumerable.Range(0, _TOTAL).Select(i => new HashEntry($"f-{i}", $"v-{i}"))];
        await _database.HashSetAsync(key, entries).ConfigureAwait(true);

        var (_, items, total) = await CollectAsync(key, RedisType.Hash).ConfigureAwait(true);

        Assert.Equal(_TOTAL, items.Count);
        Assert.Equal(_TOTAL, items.Select(x => x.Field).Distinct().Count());
        Assert.All(items, x => Assert.Equal("v-" + x.Field!["f-".Length..], x.Value));
        Assert.Equal(_TOTAL, total);
    }

    /// <summary>
    ///     连接测试 Redis，未配置时保持空连接
    /// </summary>
    /// <returns>异步初始化任务</returns>
    public async Task InitializeAsync() {
        var endpoint = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return;
        }

        _redis = await ConnectionMultiplexer.ConnectAsync(endpoint).ConfigureAwait(false);
        _database = _redis.GetDatabase();
        _prefix = $"reader-test:{Guid.NewGuid():N}:";
    }

    /// <summary>
    ///     验证 List 按下标分页：共 1250 项每页 100，应读 13 页，元素顺序与下标一致，最后一页无下一页游标
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task List_PagesByIndexWithoutGapsAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "list";
        RedisValue[] values = [.. Enumerable.Range(0, _TOTAL).Select(i => (RedisValue)$"item-{i}")];
        _ = await _database.ListRightPushAsync(key, values).ConfigureAwait(true);

        var (pages, items, total) = await CollectAsync(key, RedisType.List).ConfigureAwait(true);

        Assert.Equal(13, pages);
        Assert.Equal(_TOTAL, items.Count);
        Assert.Equal(Enumerable.Range(0, _TOTAL).Select(i => $"item-{i}"), items.Select(x => x.Value));
        Assert.Equal(Enumerable.Range(0, _TOTAL).Select(i => (long)i), items.Select(x => x.Index));
        Assert.Equal(_TOTAL, total);
    }

    /// <summary>
    ///     验证单个元素超过长度上限时被截断并标记
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task LongItemIsTruncatedAndFlaggedAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "list";
        _ = await _database.ListRightPushAsync(key, new string('a', RedisItemReader.MAX_ITEM_LENGTH + 500)).ConfigureAwait(true);

        var page = await RedisItemReader.ReadAsync(_database, key, RedisType.List, null, _PAGE).ConfigureAwait(true);

        Assert.True(page!.Truncated);
        Assert.Equal(RedisItemReader.MAX_ITEM_LENGTH, page.Items[0].Value.Length);
    }

    /// <summary>
    ///     验证 Set 用 SSCAN 分页：所有成员都取到且不重复
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Set_ScansAllMembersOnceAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "set";
        RedisValue[] values = [.. Enumerable.Range(0, _TOTAL).Select(i => (RedisValue)$"s-{i}")];
        _ = await _database.SetAddAsync(key, values).ConfigureAwait(true);

        var (_, items, _) = await CollectAsync(key, RedisType.Set).ConfigureAwait(true);

        Assert.Equal(_TOTAL, items.Count);
        Assert.Equal(_TOTAL, items.Select(x => x.Value).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(0, _TOTAL).Select(i => $"s-{i}").Order(StringComparer.Ordinal), items.Select(x => x.Value).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    ///     验证 SortedSet 按排名分页：分数从小到大，分数与成员对应
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task SortedSet_PagesByRankWithScoresAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "zset";
        SortedSetEntry[] entries = [.. Enumerable.Range(0, _TOTAL).Select(i => new SortedSetEntry($"m-{i}", i * 1.5))];
        _ = await _database.SortedSetAddAsync(key, entries).ConfigureAwait(true);

        var (_, items, _) = await CollectAsync(key, RedisType.SortedSet).ConfigureAwait(true);

        Assert.Equal(_TOTAL, items.Count);
        Assert.Equal(Enumerable.Range(0, _TOTAL).Select(i => $"m-{i}"), items.Select(x => x.Value));
        Assert.Equal(Enumerable.Range(0, _TOTAL).Select(i => i * 1.5), items.Select(x => x.Score!.Value));
    }

    /// <summary>
    ///     验证 Stream 按条目 ID 分页：游标是上一页最后一个条目 ID（不含），条目字段序列化为 JSON，不重复不遗漏
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Stream_PagesByEntryIdAsync() {
        if (_redis is null) {
            return;
        }

        var key = _prefix + "stream";
        for (var i = 0; i < 250; i++) {
            _ = await _database.StreamAddAsync(key, [new NameValueEntry("n", i), new NameValueEntry("tag", "x")]).ConfigureAwait(true);
        }

        var (_, items, total) = await CollectAsync(key, RedisType.Stream).ConfigureAwait(true);

        Assert.Equal(250, items.Count);
        Assert.Equal(250, items.Select(x => x.Field).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 250).Select(i => $"{{\"n\":\"{i}\",\"tag\":\"x\"}}"), items.Select(x => x.Value));
        Assert.Equal(250, total);
    }

    /// <summary>
    ///     验证不受支持的类型（字符串）返回空，每页数量被限制在上限内
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task UnsupportedTypeReturnsNullAndPageSizeIsClampedAsync() {
        if (_redis is null) {
            return;
        }

        var text = _prefix + "string";
        _ = await _database.StringSetAsync(text, "hello").ConfigureAwait(true);
        Assert.Null(await RedisItemReader.ReadAsync(_database, text, RedisType.String, null, _PAGE).ConfigureAwait(true));

        var list = _prefix + "list";
        RedisValue[] values = [.. Enumerable.Range(0, 800).Select(i => (RedisValue)i)];
        _ = await _database.ListRightPushAsync(list, values).ConfigureAwait(true);
        var page = await RedisItemReader.ReadAsync(_database, list, RedisType.List, null, 100_000).ConfigureAwait(true);
        Assert.Equal(RedisItemReader.MAX_PAGE_SIZE, page!.Items.Count);
        Assert.NotNull(page.NextCursor);
    }

    /// <summary>
    ///     从头开始连续翻页直到没有下一页游标，汇总所有元素
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="type">数据类型</param>
    /// <returns>页数、全部元素和总数</returns>
    private async Task<(int Pages, List<RedisCacheItemResult> Items, long Total)> CollectAsync(
        string key
        , RedisType type
    ) {
        var items = new List<RedisCacheItemResult>();
        string? cursor = null;
        var pages = 0;
        long total;
        do {
            var page = await RedisItemReader.ReadAsync(_database, key, type, cursor, _PAGE).ConfigureAwait(false);
            Assert.NotNull(page);
            items.AddRange(page.Items);
            total = page.Total;
            cursor = page.NextCursor;
            pages++;
            Assert.True(pages < 1000, "pagination did not terminate");
        } while (cursor is not null);

        return (pages, items, total);
    }
}