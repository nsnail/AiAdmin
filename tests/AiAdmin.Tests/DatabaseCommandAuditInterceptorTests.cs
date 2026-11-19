using AiAdmin.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证数据库命令审计只记录达到慢 SQL 阈值的命令
/// </summary>
public sealed class DatabaseCommandAuditInterceptorTests
{
    /// <summary>
    ///     验证内存数据库上的快速命令低于阈值时不产生审计日志，避免每条 SQL 都进入日志管道
    /// </summary>
    [Fact]
    public async Task FastCommands_AreNotLoggedAsync() {
        var logger = new CountingLogger();

        await RunQueriesAsync(logger, 60_000).ConfigureAwait(true);

        Assert.Equal(0, logger.Count);
    }

    /// <summary>
    ///     验证阈值为 0 时所有命令都会记录
    /// </summary>
    [Fact]
    public async Task ZeroThreshold_LogsEveryCommandAsync() {
        var logger = new CountingLogger();

        await RunQueriesAsync(logger, 0).ConfigureAwait(true);

        Assert.True(logger.Count > 0);
    }

    /// <summary>
    ///     使用指定阈值的拦截器在内存 SQLite 上执行几条命令
    /// </summary>
    /// <param name="logger">计数日志记录器</param>
    /// <param name="thresholdMilliseconds">慢 SQL 阈值</param>
    /// <returns>异步任务</returns>
    private static async Task RunQueriesAsync(
        CountingLogger logger
        , int thresholdMilliseconds
    ) {
        var interceptor = new DatabaseCommandAuditInterceptor(
            logger, Options.Create(new DatabaseAuditOptions { SlowThresholdMilliseconds = thresholdMilliseconds })
        );
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);
        var builder = new DbContextOptionsBuilder<AuditContext>().UseSqlite(connection).AddInterceptors(interceptor);
        await using var context = new AuditContext(builder.Options);
        for (var i = 0; i < 3; i++) {
            _ = await context.Database.ExecuteSqlRawAsync("SELECT 1").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     仅用于执行原始 SQL 的空上下文
    /// </summary>
    /// <param name="options">上下文配置</param>
    private sealed class AuditContext(DbContextOptions<AuditContext> options) : DbContext(options);

    /// <summary>
    ///     只统计 Information 级别日志条数的日志记录器
    /// </summary>
    private sealed class CountingLogger : ILogger<DatabaseCommandAuditInterceptor>
    {
        private int _count;

        /// <summary>
        ///     已记录的日志条数
        /// </summary>
        public int Count => Volatile.Read(ref _count);

        /// <summary>
        ///     不使用作用域
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="state">状态</param>
        /// <returns>空作用域</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull {
            return null;
        }

        /// <summary>
        ///     所有级别都启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>总是 true</returns>
        public bool IsEnabled(LogLevel logLevel) {
            return true;
        }

        /// <summary>
        ///     记录一条日志时计数
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="logLevel">日志级别</param>
        /// <param name="eventId">事件编号</param>
        /// <param name="state">状态</param>
        /// <param name="exception">异常</param>
        /// <param name="formatter">格式化函数</param>
        public void Log<TState>(
            LogLevel logLevel
            , EventId eventId
            , TState state
            , Exception? exception
            , Func<TState, Exception?, string> formatter
        ) {
            _ = Interlocked.Increment(ref _count);
        }
    }
}