using AiAdmin.Api.Middleware;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证熔断器的三态流转：关闭时统计失败率，达到阈值后打开，到期进入半开，试探成功恢复、失败重新打开。
///     需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:6379），未设置时每个测试直接返回，不执行任何断言
/// </summary>
public sealed class CircuitBreakerTests : IAsyncLifetime
{
    private CircuitBreaker _breaker = null!;
    private ConnectionMultiplexer? _redis;
    private long _ruleId;

    /// <summary>
    ///     删除测试写入的键并断开连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_redis is null) {
            return;
        }

        _ = await _redis.GetDatabase().KeyDeleteAsync($"aiadmin:circuit-breaker:{_ruleId}").ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     打开到期后进入半开，只放行配额内的试探请求，全部成功后恢复关闭
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task HalfOpenRecoversAfterAllProbesSucceedAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(2, 50, 1, 2);
        _ = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        Assert.Equal(CircuitBreakerState.Open, await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true));
        Assert.False((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Allowed);

        await Task.Delay(1200).ConfigureAwait(true);
        var first = await _breaker.TryAcquireAsync(rule).ConfigureAwait(true);
        var second = await _breaker.TryAcquireAsync(rule).ConfigureAwait(true);
        var third = await _breaker.TryAcquireAsync(rule).ConfigureAwait(true);
        Assert.True(first is { Allowed: true, Probe: true, State: CircuitBreakerState.HalfOpen });
        Assert.True(second is { Allowed: true, Probe: true });
        Assert.False(third.Allowed);

        Assert.Equal(CircuitBreakerState.HalfOpen, await _breaker.ReportAsync(rule, false, true).ConfigureAwait(true));
        Assert.Equal(CircuitBreakerState.Closed, await _breaker.ReportAsync(rule, false, true).ConfigureAwait(true));
        Assert.True((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Allowed);
    }

    /// <summary>
    ///     半开状态下任何一个试探请求失败，立即重新打开并重新计时
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task HalfOpenReopensWhenAProbeFailsAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(2, 50, 1);
        _ = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        _ = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        await Task.Delay(1200).ConfigureAwait(true);
        Assert.True((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Probe);

        Assert.Equal(CircuitBreakerState.Open, await _breaker.ReportAsync(rule, true, true).ConfigureAwait(true));
        Assert.False((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Allowed);
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
        _breaker = new CircuitBreaker(_redis);
        _ruleId = Random.Shared.NextInt64(1_000_000_000L, long.MaxValue);
    }

    /// <summary>
    ///     慢调用阈值内的响应、4xx 和 429 都不算失败，5xx、异常和超过阈值的慢调用算失败
    /// </summary>
    /// <param name="statusCode">响应状态码</param>
    /// <param name="threw">是否抛出异常</param>
    /// <param name="elapsedMs">耗时毫秒数</param>
    /// <param name="slowCallMs">慢调用阈值</param>
    /// <param name="expected">期望是否算失败</param>
    [Theory]
    [InlineData(200, false, 10, 0, false)]
    [InlineData(404, false, 10, 0, false)]
    [InlineData(500, false, 10, 0, true)]
    [InlineData(503, false, 10, 0, true)]
    [InlineData(200, true, 10, 0, true)]
    [InlineData(200, false, 5000, 0, false)]
    [InlineData(200, false, 5000, 1000, true)]
    [InlineData(200, false, 500, 1000, false)]
    public void IsFailure_FollowsStatusAndLatency(
        int statusCode
        , bool threw
        , long elapsedMs
        , int slowCallMs
        , bool expected
    ) {
        Assert.Equal(expected, CircuitBreakerMiddleware.IsFailure(statusCode, threw, elapsedMs, slowCallMs));
    }

    /// <summary>
    ///     达到最小样本量且失败率达到阈值后打开，之后的请求被拒绝并给出等待时间
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task OpensWhenFailureRateReachesThresholdAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(10, 50);
        for (var i = 0; i < 5; i++) {
            _ = await _breaker.ReportAsync(rule, false, false).ConfigureAwait(true);
        }

        var state = CircuitBreakerState.Closed;
        for (var i = 0; i < 5; i++) {
            state = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        }

        Assert.Equal(CircuitBreakerState.Open, state);
        var permit = await _breaker.TryAcquireAsync(rule).ConfigureAwait(true);
        Assert.False(permit.Allowed);
        Assert.Equal(CircuitBreakerState.Open, permit.State);
        Assert.InRange(permit.RetryAfterSeconds, 1, 30);
    }

    /// <summary>
    ///     恢复关闭后重新统计，旧的失败记录不会让它立刻再次熔断
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task RecoveryClearsOldStatisticsAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(4, 50, 1, 1);
        for (var i = 0; i < 4; i++) {
            _ = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        }

        await Task.Delay(1200).ConfigureAwait(true);
        Assert.True((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Probe);
        Assert.Equal(CircuitBreakerState.Closed, await _breaker.ReportAsync(rule, false, true).ConfigureAwait(true));

        // 恢复后只来 3 个失败，没到最小样本量，不应该再熔断
        for (var i = 0; i < 3; i++) {
            Assert.Equal(CircuitBreakerState.Closed, await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true));
        }
    }

    /// <summary>
    ///     失败率没达到阈值时保持关闭
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task StaysClosedBelowFailureRateAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(10, 50);
        for (var i = 0; i < 7; i++) {
            _ = await _breaker.ReportAsync(rule, false, false).ConfigureAwait(true);
        }

        var state = CircuitBreakerState.Closed;
        for (var i = 0; i < 3; i++) {
            state = await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true);
        }

        Assert.Equal(CircuitBreakerState.Closed, state);
        Assert.True((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Allowed);
    }

    /// <summary>
    ///     请求数没到最小样本量时，即使全部失败也不熔断，避免低流量时误熔断
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task StaysClosedBelowMinimumRequestsAsync() {
        if (_redis is null) {
            return;
        }

        var rule = Rule(10, 50);
        for (var i = 0; i < 9; i++) {
            Assert.Equal(CircuitBreakerState.Closed, await _breaker.ReportAsync(rule, true, false).ConfigureAwait(true));
        }

        Assert.True((await _breaker.TryAcquireAsync(rule).ConfigureAwait(true)).Allowed);
    }

    /// <summary>
    ///     创建测试用熔断规则
    /// </summary>
    /// <param name="minimumRequests">评估失败率所需的最小请求数</param>
    /// <param name="failureRatePercent">触发熔断的失败率百分比</param>
    /// <param name="openSeconds">打开状态持续秒数</param>
    /// <param name="halfOpenRequests">半开状态试探请求数</param>
    /// <returns>熔断规则快照</returns>
    private CircuitBreakerRuleSnapshot Rule(
        int minimumRequests
        , int failureRatePercent
        , int openSeconds = 30
        , int halfOpenRequests = 3
    ) {
        return new CircuitBreakerRuleSnapshot(_ruleId, failureRatePercent, minimumRequests, 60, openSeconds, halfOpenRequests, 0);
    }
}