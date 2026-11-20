using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证限流和熔断规则缓存在读库失败时不会把故障放大成每个请求一次失败查询：
///     失败后进入退避期，期间不再访问数据库，按没有规则放行，只记一条日志；故障恢复后能重新加载
/// </summary>
public sealed class RuleCacheFailureTests
{
    /// <summary>
    ///     退避：失败后进入退避期，每个退避周期只有第一次失败返回 true，成功后立即结束退避
    /// </summary>
    [Fact]
    public void Backoff_EntersAfterFailureAndEndsOnSuccess() {
        var backoff = new RuleLoadBackoff(TimeSpan.FromMinutes(1));
        Assert.False(backoff.IsBackingOff);

        Assert.True(backoff.Fail());
        Assert.True(backoff.IsBackingOff);

        // 同一退避周期内的后续失败不再返回 true，调用方据此只记一条日志
        Assert.False(backoff.Fail());
        Assert.True(backoff.IsBackingOff);

        backoff.Succeed();
        Assert.False(backoff.IsBackingOff);
    }

    /// <summary>
    ///     退避期到期后可以再次尝试
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Backoff_ExpiresAfterDurationAsync() {
        var backoff = new RuleLoadBackoff(TimeSpan.FromMilliseconds(150));
        Assert.True(backoff.Fail());
        Assert.True(backoff.IsBackingOff);

        await Task.Delay(300).ConfigureAwait(true);
        Assert.False(backoff.IsBackingOff);
        Assert.True(backoff.Fail());
    }

    /// <summary>
    ///     熔断规则表不存在时同样只访问一次数据库
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task CircuitBreakerCache_DoesNotHitDatabaseAgainWhileBackingOffAsync() {
        var counter = new CountingFactory();
        var ruleCache = new CircuitBreakerRuleCache(NewCache(), counter, NullLogger<CircuitBreakerRuleCache>.Instance);

        for (var i = 0; i < 50; i++) {
            Assert.Empty(await ruleCache.GetAsync().ConfigureAwait(true));
        }

        Assert.Equal(1, counter.Created);
    }

    /// <summary>
    ///     限流规则表不存在时：读库失败后返回空规则，退避期内的大量并发请求不会再次访问数据库
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task RateLimitCache_DoesNotHitDatabaseAgainWhileBackingOffAsync() {
        var counter = new CountingFactory();
        var ruleCache = new RateLimitRuleCache(NewCache(), counter, NullLogger<RateLimitRuleCache>.Instance);

        for (var i = 0; i < 50; i++) {
            Assert.Empty(await ruleCache.GetAsync().ConfigureAwait(true));
        }

        Assert.Equal(1, counter.Created);
    }

    /// <summary>
    ///     故障恢复后（表存在、有已启用规则）能成功加载并缓存，之后的请求直接读缓存，不再访问数据库
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task RateLimitCache_LoadsOnceDatabaseIsHealthyAndThenServesFromCacheAsync() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync().ConfigureAwait(true);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var setup = new AppDbContext(options, new DataScopeContext(), null!)) {
            _ = await setup.Database.EnsureCreatedAsync().ConfigureAwait(true);
            var endpoint = new ApiEndpoint
            {
                Method = "POST"
                , Path = "/api/x"
                , Name = "X"
                , Controller = "C"
                , ControllerName = "C"
                , Action = "A"
            };
            _ = setup.ApiEndpoints.Add(endpoint);
            _ = setup.RateLimitRules.Add(
                new RateLimitRule
                {
                    ApiEndpoint = endpoint
                    , ApiEndpointId = endpoint.Id
                    , Scope = RateLimitScope.Global
                    , Limit = 5
                    , WindowSeconds = 1
                    , IsEnabled = true
                }
            );
            _ = await setup.SaveChangesAsync().ConfigureAwait(true);
        }

        var counter = new CountingFactory(options);
        var ruleCache = new RateLimitRuleCache(NewCache(), counter, NullLogger<RateLimitRuleCache>.Instance);

        var first = await ruleCache.GetAsync().ConfigureAwait(true);
        _ = Assert.Single(first);
        for (var i = 0; i < 20; i++) {
            _ = Assert.Single(await ruleCache.GetAsync().ConfigureAwait(true));
        }

        Assert.Equal(1, counter.Created);
    }

    /// <summary>
    ///     创建进程内的分布式缓存
    /// </summary>
    /// <returns>分布式缓存</returns>
    private static MemoryDistributedCache NewCache() {
        return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    /// <summary>
    ///     统计创建作用域（即访问数据库）次数的作用域工厂；不传选项时数据库上没有任何表，等价于线上表尚未创建
    /// </summary>
    /// <param name="options">数据库上下文选项，为空时使用没有任何表的空内存库</param>
    private sealed class CountingFactory(DbContextOptions<AppDbContext>? options = null) : IServiceScopeFactory
    {
        private int _created;

        /// <summary>
        ///     已创建的作用域数量
        /// </summary>
        public int Created => _created;

        /// <summary>
        ///     创建作用域并计数
        /// </summary>
        /// <returns>服务作用域</returns>
        public IServiceScope CreateScope() {
            _ = Interlocked.Increment(ref _created);
            var effective = options ?? new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
            var services = new ServiceCollection();
            _ = services.AddScoped(_ => new AppDbContext(effective, new DataScopeContext(), null!));
            return services.BuildServiceProvider().CreateScope();
        }
    }
}