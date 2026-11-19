using System.Globalization;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证告警监控读取 Redis 的值和长度：各种类型的长度语义、键不存在、值的类型限制，以及与判定器连起来的端到端结果。
///     需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:6379），未设置时每个测试直接返回，不执行任何断言
/// </summary>
public sealed class AlertMonitorRedisTests : IAsyncLifetime
{
    private readonly List<string> _keys = [];
    private IDatabase _database = null!;
    private string _prefix = string.Empty;
    private ConnectionMultiplexer? _redis;

    /// <summary>
    ///     删除测试写入的键并断开连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_redis is null) {
            return;
        }

        foreach (var key in _keys) {
            _ = await _database.KeyDeleteAsync(key).ConfigureAwait(false);
        }

        await _redis.DisposeAsync().ConfigureAwait(false);
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
        _prefix = $"alert-monitor-test:{Guid.NewGuid():N}:";
    }

    /// <summary>
    ///     长度语义：字符串为字节数，List、Set、Hash、SortedSet 为元素个数，键不存在为 0
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task LengthFollowsTheTypeOfTheKeyAsync() {
        if (_redis is null) {
            return;
        }

        var text = Track("str");
        var list = Track("list");
        var set = Track("set");
        var hash = Track("hash");
        var zset = Track("zset");
        _ = await _database.StringSetAsync(text, "hello").ConfigureAwait(true);
        _ = await _database.ListRightPushAsync(list, ["a", "b", "c", "d"]).ConfigureAwait(true);
        _ = await _database.SetAddAsync(set, ["x", "y"]).ConfigureAwait(true);
        await _database.HashSetAsync(hash, [new HashEntry("f1", "v1"), new HashEntry("f2", "v2"), new HashEntry("f3", "v3")]).ConfigureAwait(true);
        _ = await _database.SortedSetAddAsync(zset, [new SortedSetEntry("a", 1), new SortedSetEntry("b", 2)]).ConfigureAwait(true);

        Assert.Equal(5, await AlertMonitorScanService.ReadRedisLengthAsync(_database, text).ConfigureAwait(true));
        Assert.Equal(4, await AlertMonitorScanService.ReadRedisLengthAsync(_database, list).ConfigureAwait(true));
        Assert.Equal(2, await AlertMonitorScanService.ReadRedisLengthAsync(_database, set).ConfigureAwait(true));
        Assert.Equal(3, await AlertMonitorScanService.ReadRedisLengthAsync(_database, hash).ConfigureAwait(true));
        Assert.Equal(2, await AlertMonitorScanService.ReadRedisLengthAsync(_database, zset).ConfigureAwait(true));
        Assert.Equal(0, await AlertMonitorScanService.ReadRedisLengthAsync(_database, Track("missing")).ConfigureAwait(true));
    }

    /// <summary>
    ///     长度监控端到端：队列积压超过阈值触发，被消费到阈值以内解除；队列被清空（键消失）时按小于等于 0 判定触发
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task QueueBacklogRaisesAndDrainingResolvesAsync() {
        if (_redis is null) {
            return;
        }

        var key = Track("queue");
        var monitor = new AlertMonitor
        {
            Name = "queue"
            , Target = key
            , Source = AlertMonitorSource.Redis
            , Metric = AlertMonitorMetric.Length
            , ValueType = AlertMonitorValueType.Number
            , Operator = AlertMonitorOperator.GreaterThan
            , Threshold = "3"
        };
        _ = await _database.ListRightPushAsync(key, ["1", "2", "3", "4", "5"]).ConfigureAwait(true);

        var backlog = await EvaluateLengthAsync(monitor, key).ConfigureAwait(true);
        Assert.True(backlog.Breached);
        Assert.Equal("5", backlog.Value);

        _ = await _database.ListLeftPopAsync(key, 3).ConfigureAwait(true);
        Assert.False((await EvaluateLengthAsync(monitor, key).ConfigureAwait(true)).Breached);

        monitor.Operator = AlertMonitorOperator.LessThanOrEqual;
        monitor.Threshold = "0";
        _ = await _database.KeyDeleteAsync(key).ConfigureAwait(true);
        Assert.True((await EvaluateLengthAsync(monitor, key).ConfigureAwait(true)).Breached);
    }

    /// <summary>
    ///     值指标：字符串键读出文本，键不存在和非字符串类型都视为取不到值并说明原因
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ValueReadsStringsAndRejectsMissingAndNonStringKeysAsync() {
        if (_redis is null) {
            return;
        }

        var text = Track("value");
        var list = Track("list");
        _ = await _database.StringSetAsync(text, "42.5").ConfigureAwait(true);
        _ = await _database.ListRightPushAsync(list, "a").ConfigureAwait(true);

        Assert.Equal(("42.5", string.Empty), await AlertMonitorScanService.ReadRedisValueAsync(_database, text).ConfigureAwait(true));
        Assert.Equal(
            (null, "Redis key does not exist"), await AlertMonitorScanService.ReadRedisValueAsync(_database, Track("missing")).ConfigureAwait(true)
        );
        Assert.Equal((null, "Redis key is not a string"), await AlertMonitorScanService.ReadRedisValueAsync(_database, list).ConfigureAwait(true));
    }

    /// <summary>
    ///     读取键的长度并交给判定器，等价于扫描服务对 Redis 长度监控的处理
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="key">缓存键</param>
    /// <returns>判定结果</returns>
    private async Task<AlertMonitorOutcome> EvaluateLengthAsync(
        AlertMonitor monitor
        , string key
    ) {
        var length = await AlertMonitorScanService.ReadRedisLengthAsync(_database, key).ConfigureAwait(false);
        return AlertMonitorEvaluator.Evaluate(monitor, length.ToString(CultureInfo.InvariantCulture), string.Empty);
    }

    /// <summary>
    ///     生成带唯一前缀的测试键并登记，测试结束时统一删除
    /// </summary>
    /// <param name="name">键名后缀</param>
    /// <returns>完整的测试键</returns>
    private string Track(string name) {
        var key = _prefix + name;
        _keys.Add(key);
        return key;
    }
}