using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AiAdmin.Api.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证 Elasticsearch 日志消费后台任务在读取 Redis 队列失败时不会让任务崩溃
/// </summary>
[Collection("ShutdownGate")]
public sealed class ElasticsearchLogBackgroundServiceTests
{
    /// <summary>
    ///     验证队列读取抛出 Redis 超时异常时后台任务继续运行，不会因未处理异常停止宿主
    /// </summary>
    [Fact]
    public async Task Worker_SurvivesQueueReadFailureAsync() {
        var settings = new ElasticsearchLogOptions { Enabled = true, FlushInterval = TimeSpan.FromMilliseconds(10), QueueKey = "test:es" };
        var queue = new ElasticsearchLogQueue(ThrowingMultiplexer.Create(), settings);
        using var httpClient = new HttpClient();
        var writer = new ElasticsearchLogWriter(httpClient, Options.Create(settings), null!);
        using var service = new ElasticsearchLogBackgroundService(
            queue, writer, Options.Create(settings), NullLogger<ElasticsearchLogBackgroundService>.Instance
        );

        await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await Task.Delay(300).ConfigureAwait(true);

        Assert.True(service.ExecuteTask is { IsFaulted: false, IsCompleted: false });
        await service.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    ///     所有调用都抛出 Redis 超时异常的数据库代理
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class ThrowingDatabase : DispatchProxy
    {
        /// <summary>
        ///     模拟 Redis 超时
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>不会正常返回</returns>
        /// <exception cref="RedisTimeoutException">总是引发</exception>
        protected override object Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            throw new RedisTimeoutException(CommandFlags.None, "simulated timeout", CommandStatus.Unknown);
        }
    }

    /// <summary>
    ///     所有 Redis 调用都抛出超时异常的连接复用器和数据库代理
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class ThrowingMultiplexer : DispatchProxy
    {
        private static readonly IDatabase _database = Create<IDatabase, ThrowingDatabase>();

        /// <summary>
        ///     创建连接复用器代理
        /// </summary>
        /// <returns>连接复用器代理</returns>
        public static IConnectionMultiplexer Create() {
            return Create<IConnectionMultiplexer, ThrowingMultiplexer>();
        }

        /// <summary>
        ///     GetDatabase 返回总是抛异常的数据库，其余返回默认值
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return targetMethod!.Name == nameof(IConnectionMultiplexer.GetDatabase) ? _database : null;
        }
    }
}