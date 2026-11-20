using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证接口幂等存储：首次占位、处理中拒绝、成功后回放、内容不一致拒绝、放弃后可重试。
///     需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:6379），未设置时每个测试直接返回，不执行任何断言
/// </summary>
public sealed class IdempotencyServiceTests : IAsyncLifetime
{
    private readonly List<string> _keys = [];
    private ConnectionMultiplexer? _redis;
    private IdempotencyService _service = null!;

    /// <summary>
    ///     验证首次占位成功，处理中的相同请求被拒绝，放弃占位后可以重新开始
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task BeginAsync_RejectsConcurrentDuplicateUntilAbandonedAsync() {
        if (_redis is null) {
            return;
        }

        var key = Track("lock");
        var ttl = TimeSpan.FromSeconds(30);
        Assert.Equal(IdempotencyState.Started, (await _service.BeginAsync(key, "f1", ttl).ConfigureAwait(true)).State);
        Assert.Equal(IdempotencyState.InProgress, (await _service.BeginAsync(key, "f1", ttl).ConfigureAwait(true)).State);

        await _service.AbandonAsync(key).ConfigureAwait(true);
        Assert.Equal(IdempotencyState.Started, (await _service.BeginAsync(key, "f1", ttl).ConfigureAwait(true)).State);
    }

    /// <summary>
    ///     验证成功后相同请求回放首次响应，同一个键用于不同内容时被拒绝
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task BeginAsync_ReplaysCompletedResponseAndRejectsMismatchAsync() {
        if (_redis is null) {
            return;
        }

        var key = Track("done");
        var ttl = TimeSpan.FromSeconds(30);
        _ = await _service.BeginAsync(key, "f1", ttl).ConfigureAwait(true);
        await _service.CompleteAsync(key, "f1", 200, "{\"code\":200}", ttl).ConfigureAwait(true);

        var replay = await _service.BeginAsync(key, "f1", ttl).ConfigureAwait(true);
        Assert.Equal(IdempotencyState.Replay, replay.State);
        Assert.Equal(200, replay.Entry!.StatusCode);
        Assert.Equal("{\"code\":200}", replay.Entry.Body);
        Assert.Equal(IdempotencyState.Mismatch, (await _service.BeginAsync(key, "f2", ttl).ConfigureAwait(true)).State);
    }

    /// <summary>
    ///     验证相同内容的指纹稳定，不同内容的指纹不同
    /// </summary>
    [Fact]
    public void ComputeFingerprint_IsStableAndContentSensitive() {
        Assert.Equal(IdempotencyService.ComputeFingerprint("a"), IdempotencyService.ComputeFingerprint("a"));
        Assert.NotEqual(IdempotencyService.ComputeFingerprint("a"), IdempotencyService.ComputeFingerprint("b"));
    }

    /// <summary>
    ///     删除测试写入的键并断开连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_redis is null) {
            return;
        }

        var database = _redis.GetDatabase();
        foreach (var key in _keys) {
            _ = await database.KeyDeleteAsync(key).ConfigureAwait(false);
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
        _service = new IdempotencyService(_redis);
    }

    /// <summary>
    ///     生成带唯一后缀的测试键并登记以便清理
    /// </summary>
    /// <param name="name">键名称</param>
    /// <returns>完整键</returns>
    private string Track(string name) {
        var key = $"{IdempotencyService.KEY_PREFIX}test:{Guid.NewGuid():N}:{name}";
        _keys.Add(key);
        return key;
    }
}