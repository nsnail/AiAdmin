using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证就绪探测：数据库和 Redis 都应答才算就绪，任一不可用或已被取消都按未就绪处理且不抛出异常。
///     数据库部分使用内存 SQLite；Redis 部分需要真实 Redis，通过环境变量 TEST_REDIS 指定（例如 127.0.0.1:6379），未设置时对应测试直接返回
/// </summary>
public sealed class ReadinessProbeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReadinessProbeTests" /> class.
    ///     创建内存数据库
    /// </summary>
    public ReadinessProbeTests() {
        var connectionString = $"DataSource=file:readiness-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    }

    /// <summary>
    ///     释放内存数据库连接
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
    }

    /// <summary>
    ///     调用方已取消时视为未就绪，不抛出异常，保证接口总能返回 503 而不是 500
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task NotReadyWhenCancelledAsync() {
        var endpoint = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return;
        }

        await using var redis = await ConnectionMultiplexer.ConnectAsync(endpoint).ConfigureAwait(true);
        await using var db = NewDb();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        var result = await ReadinessProbe.CheckAsync(db, redis, cancelled.Token).ConfigureAwait(true);

        Assert.False(result.Ready);
    }

    /// <summary>
    ///     数据库连接被释放后不可用，探测返回未就绪而不是抛出异常
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task NotReadyWhenDatabaseIsUnavailableAsync() {
        var endpoint = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return;
        }

        await using var redis = await ConnectionMultiplexer.ConnectAsync(endpoint).ConfigureAwait(true);
        var badOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=/nonexistent-dir/none.db;Mode=ReadOnly").Options;
        await using var db = new AppDbContext(badOptions, AllDataScope(), new DataScopeCache(redis));

        var result = await ReadinessProbe.CheckAsync(db, redis, CancellationToken.None).ConfigureAwait(true);

        // 只有数据库失败而 Redis 正常，说明失败确实来自数据库探测，而不是整体被误判
        Assert.False(result.Ready);
        Assert.InRange(result.CacheMilliseconds, 0, 3000);
    }

    /// <summary>
    ///     数据库和 Redis 都可用时就绪，并返回各自的耗时
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ReadyWhenDatabaseAndRedisRespondAsync() {
        var endpoint = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return;
        }

        await using var redis = await ConnectionMultiplexer.ConnectAsync(endpoint).ConfigureAwait(true);
        await using var db = NewDb();

        var result = await ReadinessProbe.CheckAsync(db, redis, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.Ready);
        Assert.InRange(result.DatabaseMilliseconds, 0, 3000);
        Assert.InRange(result.CacheMilliseconds, 0, 3000);
    }

    /// <summary>
    ///     创建拥有全部数据权限的上下文
    /// </summary>
    /// <returns>数据权限上下文</returns>
    private static DataScopeContext AllDataScope() {
        var scope = new DataScopeContext();
        scope.Initialize(1, true, false, new HashSet<long>(), 100);
        return scope;
    }

    /// <summary>
    ///     创建绑定内存数据库的应用数据库上下文，Redis 缓存使用调用方传入的连接
    /// </summary>
    /// <returns>应用数据库上下文</returns>
    private AppDbContext NewDb() {
        return new AppDbContext(
            _options, AllDataScope(), new DataScopeCache(ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("TEST_REDIS")!))
        );
    }
}