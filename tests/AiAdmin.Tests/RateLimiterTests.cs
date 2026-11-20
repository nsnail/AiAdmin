using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证滑动窗口、漏桶和令牌桶三种限流算法的放行、拒绝和恢复行为，以及不同主体、不同规则之间互不影响。
///     需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:6379），未设置时每个测试直接返回，不执行任何断言
/// </summary>
public sealed class RateLimiterTests : IAsyncLifetime
{
    private RateLimiter _limiter = null!;
    private ConnectionMultiplexer? _redis;
    private long _ruleId;

    /// <summary>
    ///     窗口和桶按速率恢复，等待一个速率周期后重新放行
    /// </summary>
    /// <param name="algorithm">限流算法</param>
    /// <returns>异步测试任务</returns>
    [Theory]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.LeakyBucket)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    public async Task AllowsAgainAfterTheRateRecoversAsync(RateLimitAlgorithm algorithm) {
        if (_redis is null) {
            return;
        }

        // 每秒 2 个，容量 2：用完后约 0.5 秒补回一个，滑动窗口则约 1 秒后窗口内的旧请求滑出
        var rule = new RateLimitRuleSnapshot(_ruleId, algorithm, RateLimitScope.Global, null, null, 2, 1);
        Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
        Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
        Assert.False((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);

        await Task.Delay(1100).ConfigureAwait(true);
        Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
    }

    /// <summary>
    ///     不同主体各自计数，一个用户被限流不影响另一个用户
    /// </summary>
    /// <param name="algorithm">限流算法</param>
    /// <returns>异步测试任务</returns>
    [Theory]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.LeakyBucket)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    public async Task CountsEachSubjectSeparatelyAsync(RateLimitAlgorithm algorithm) {
        if (_redis is null) {
            return;
        }

        var rule = new RateLimitRuleSnapshot(_ruleId, algorithm, RateLimitScope.User, null, null, 1, 60);
        Assert.True((await _limiter.CheckAsync(rule, "user:1").ConfigureAwait(true)).Allowed);
        Assert.False((await _limiter.CheckAsync(rule, "user:1").ConfigureAwait(true)).Allowed);
        Assert.True((await _limiter.CheckAsync(rule, "user:2").ConfigureAwait(true)).Allowed);
    }

    /// <summary>
    ///     删除测试写入的限流键并断开连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_redis is null) {
            return;
        }

        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        await foreach (var key in server.KeysAsync(pattern: $"aiadmin:rate-limit:{_ruleId}:*").ConfigureAwait(false)) {
            _ = await _redis.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
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
        _limiter = new RateLimiter(_redis);

        // 每个测试用独立的规则编号，键互不干扰
        _ruleId = Random.Shared.NextInt64(1_000_000_000L, long.MaxValue);
    }

    /// <summary>
    ///     三种算法在容量内全部放行，超过容量后拒绝，并给出至少 1 秒的建议等待时间
    /// </summary>
    /// <param name="algorithm">限流算法</param>
    /// <returns>异步测试任务</returns>
    [Theory]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.LeakyBucket)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    public async Task RejectsRequestsBeyondTheLimitAsync(RateLimitAlgorithm algorithm) {
        if (_redis is null) {
            return;
        }

        var rule = new RateLimitRuleSnapshot(_ruleId, algorithm, RateLimitScope.Global, null, null, 3, 60);
        for (var i = 0; i < 3; i++) {
            Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
        }

        var rejected = await _limiter.CheckAsync(rule, "global").ConfigureAwait(true);
        Assert.False(rejected.Allowed);
        Assert.InRange(rejected.RetryAfterSeconds, 1, 60);
    }

    /// <summary>
    ///     令牌桶允许桶容量内的突发而漏桶同样限制突发，二者拒绝后恢复的节奏一致；
    ///     这里验证令牌桶空闲后补满，之后可再次突发到容量
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task TokenBucketRefillsToCapacityAfterIdleAsync() {
        if (_redis is null) {
            return;
        }

        var rule = new RateLimitRuleSnapshot(_ruleId, RateLimitAlgorithm.TokenBucket, RateLimitScope.Global, null, null, 3, 1);
        for (var i = 0; i < 3; i++) {
            Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
        }

        await Task.Delay(1200).ConfigureAwait(true);
        for (var i = 0; i < 3; i++) {
            Assert.True((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
        }

        Assert.False((await _limiter.CheckAsync(rule, "global").ConfigureAwait(true)).Allowed);
    }
}