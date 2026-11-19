using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证数据库连接拦截器把连接的打开到关闭区间登记为在途工作，且在查询、事务、失败等各路径下计数都能回到零
/// </summary>
[Collection("ShutdownGate")]
public sealed class DatabaseShutdownInterceptorTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabaseShutdownInterceptorTests" /> class.
    ///     创建内存数据库并挂载拦截器
    /// </summary>
    public DatabaseShutdownInterceptorTests() {
        var connectionString = $"DataSource=file:dbshutdown-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).AddInterceptors(new DatabaseShutdownInterceptor()).Options;
        using var init = NewDb();
        _ = init.Database.EnsureCreated();
    }

    /// <summary>
    ///     并发 20 个查询全部结束后计数归零，没有重复扣减也没有泄漏
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ConcurrentQueriesBalanceAsync() {
        await Task
            .WhenAll(
                Enumerable
                    .Range(0, 20)
                    .Select(async index =>
                        {
                            await using var db = NewDb();
                            _ = await db.Users.Where(x => x.Id != index).ToListAsync().ConfigureAwait(false);
                        }
                    )
            )
            .ConfigureAwait(true);
        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     释放保持内存库存活的连接
    /// </summary>
    public void Dispose() {
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     显式打开连接后到关闭前一直算在途，被 prepare 等待
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ExplicitlyOpenedConnectionCountsUntilClosedAsync() {
        await using var db = NewDb();
        await db.Database.OpenConnectionAsync().ConfigureAwait(true);
        Assert.Equal(1, ShutdownGate.Snapshot()["Database"]);

        await db.Database.CloseConnectionAsync().ConfigureAwait(true);
        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     提交失败（主键冲突）后计数仍会归零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task FailedCommandReleasesLeaseAsync() {
        await using (var db = NewDb()) {
            _ = await Assert
                .ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("select * from table_that_does_not_exist"))
                .ConfigureAwait(true);
        }

        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     事务期间即使命令之间没有执行任何语句，连接仍被登记为在途
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task OpenTransactionKeepsDatabaseInFlightBetweenCommandsAsync() {
        var db = NewDb();
        var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(true);
        Assert.Equal(1, ShutdownGate.Snapshot()["Database"]);

        await transaction.CommitAsync().ConfigureAwait(true);
        await transaction.DisposeAsync().ConfigureAwait(true);
        await db.DisposeAsync().ConfigureAwait(true);
        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     查询结束后计数归零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task QueryReleasesLeaseAsync() {
        await using (var db = NewDb()) {
            _ = await db.Users.ToListAsync().ConfigureAwait(true);
        }

        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     事务未提交直接释放，计数仍会归零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task TransactionDisposedWithoutCommitReleasesLeaseAsync() {
        await using (var db = NewDb()) {
            await using var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(true);
            Assert.Equal(1, ShutdownGate.Snapshot()["Database"]);
        }

        Assert.Equal(0, ShutdownGate.Snapshot()["Database"]);
    }

    /// <summary>
    ///     创建带拦截器的数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    private AppDbContext NewDb() {
        return new AppDbContext(_options, new DataScopeContext(), null!);
    }
}