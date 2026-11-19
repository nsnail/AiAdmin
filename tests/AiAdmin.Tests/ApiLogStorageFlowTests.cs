using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Middleware;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证请求日志从中间件入队、后台服务批量消费到按天分表的完整链路，以及写入失败时放回队列重试
/// </summary>
[Collection("ShutdownGate")]
public sealed class ApiLogStorageFlowTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;

    private readonly ApiLogStorageOptions _options = new()
    {
        Enabled = true
        , BatchSize = 100
        , PollInterval = TimeSpan.FromMilliseconds(20)
        , QueueKey = "test:logs"
        , MaxRetries = 1000
    };

    private readonly ApiLogStorageQueue _queue;
    private readonly FakeListDatabase _redis;
    private readonly ApiRequestLogTableService _tables;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApiLogStorageFlowTests" /> class.
    ///     初始化内存数据库、内存队列和分表服务
    /// </summary>
    public ApiLogStorageFlowTests() {
        var connectionString = $"DataSource=file:api-log-flow-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _tables = new ApiRequestLogTableService("sqlite", connectionString);
        _redis = FakeListDatabase.Create();
        _queue = new ApiLogStorageQueue(FakeListDatabase.CreateMultiplexer(_redis), _options);
    }

    /// <summary>
    ///     验证反复写入失败的日志在达到重试上限后转入死信队列，不再堵住主队列
    /// </summary>
    [Fact]
    public async Task BackgroundService_MovesPoisonLogsToDeadLetterQueueAsync() {
        var options = new ApiLogStorageOptions
        {
            Enabled = true
            , BatchSize = 100
            , PollInterval = TimeSpan.FromMilliseconds(10)
            , QueueKey = "test:poison"
            , MaxRetries = 2
        };
        var queue = new ApiLogStorageQueue(FakeListDatabase.CreateMultiplexer(_redis), options);
        var broken = new ApiRequestLogTableService("sqlite", "DataSource=/nonexistent-dir/none.db");
        await queue.EnqueueAsync(NewLog("/api/x", DateTime.UtcNow)).ConfigureAwait(true);

        using (var service = new ApiLogStorageBackgroundService(
                   queue, broken, Options.Create(options), NullLogger<ApiLogStorageBackgroundService>.Instance
               )) {
            await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
            await WaitUntilAsync(() => _redis.Length("test:poison:dead") == 1).ConfigureAwait(true);
            await service.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }

        broken.Dispose();
        Assert.Equal(1, _redis.Length("test:poison:dead"));
        Assert.Equal(0, _redis.Length("test:poison"));
    }

    /// <summary>
    ///     验证写入失败时日志被放回队列，数据库恢复后继续写入且不丢失
    /// </summary>
    [Fact]
    public async Task BackgroundService_RequeuesBatchWhenDatabaseFailsAsync() {
        var day = DateTime.UtcNow;
        var broken = new ApiRequestLogTableService("sqlite", "DataSource=/nonexistent-dir/none.db");
        await _queue.EnqueueAsync(NewLog("/api/x", day)).ConfigureAwait(true);

        using (var failing = new ApiLogStorageBackgroundService(
                   _queue, broken, Options.Create(_options), NullLogger<ApiLogStorageBackgroundService>.Instance
               )) {
            await failing.StartAsync(CancellationToken.None).ConfigureAwait(true);
            await Task.Delay(300).ConfigureAwait(true);
            await failing.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }

        broken.Dispose();
        var remaining = await _queue.DequeueBatchAsync(10).ConfigureAwait(true);
        Assert.Equal(Crcs("/api/x"), remaining.Select(x => x.PathCrc32));
    }

    /// <summary>
    ///     验证后台服务把队列中的日志写入当天分表
    /// </summary>
    [Fact]
    public async Task BackgroundService_WritesQueuedLogsToDailyTableAsync() {
        var day = DateTime.UtcNow;
        for (var i = 0; i < 3; i++) {
            await _queue.EnqueueAsync(NewLog($"/api/{i}", day)).ConfigureAwait(true);
        }

        using var service = new ApiLogStorageBackgroundService(
            _queue, _tables, Options.Create(_options), NullLogger<ApiLogStorageBackgroundService>.Instance
        );
        await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() => Count(ApiRequestLogTableService.GetTableName(day)) == 3).ConfigureAwait(true);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3L, Count(ApiRequestLogTableService.GetTableName(day)));
        Assert.Empty(await _queue.DequeueBatchAsync(10).ConfigureAwait(true));
    }

    /// <summary>
    ///     释放服务和数据库连接
    /// </summary>
    public void Dispose() {
        _tables.Dispose();
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     验证中间件把请求元数据放入队列，包含方法、含查询串的路径、状态码、用户、业务码和链路编号，且不记录请求响应体
    /// </summary>
    [Fact]
    public async Task Middleware_EnqueuesRequestMetadataAsync() {
        var services = new ServiceCollection().AddSingleton(_queue).BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services
            , Request =
            {
                Method = "POST"
                , Path = "/api/user/list"
                , QueryString = new QueryString("?a=1")
                , ContentType = "application/json"
                , Headers = { UserAgent = "unit-test" }
                , Body = new MemoryStream([.. "{\"password\":\"secret\"}"u8])
            }
            , Connection = { RemoteIpAddress = IPAddress.Parse("::ffff:192.168.1.10"), LocalIpAddress = IPAddress.Parse("10.0.0.5") }
            , User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Name, "alice")], "test")
            )
        };
        var middleware = new ApiHttpLoggingMiddleware(
            async http =>
            {
                http.Response.StatusCode = 201;
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsync("{\"code\":200,\"data\":\"top-secret-response\"}").ConfigureAwait(false);
            }, NullLogger<ApiHttpLoggingMiddleware>.Instance, Options.Create(_options)
        );

        await middleware.InvokeAsync(context).ConfigureAwait(true);
        await WaitUntilAsync(() => _redis.Length("test:logs") == 1).ConfigureAwait(true);

        var log = Assert.Single(await _queue.DequeueBatchAsync(10).ConfigureAwait(true));
        Assert.Equal(ApiRequestMethod.Post, log.Method);
        Assert.Equal(ApiEndpointKey.ComputePathCrc32("/api/user/list"), log.PathCrc32);
        Assert.Equal((short)201, log.StatusCode);
        Assert.Equal((short)200, log.ApiResponseCode);
        Assert.Equal(unchecked((int)0xC0A8010A), log.ClientIp);
        Assert.Equal(0x0A000005, log.ServerIp);
        Assert.Equal(42L, log.UserId);
        Assert.NotEqual(0L, log.TraceId);
        var serialized = JsonSerializer.Serialize(log);
        Assert.DoesNotContain("secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("unit-test", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("a=1", serialized, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证入库开关关闭时中间件不入队，非 /api 请求也不入队
    /// </summary>
    [Fact]
    public async Task Middleware_SkipsWhenDisabledOrNonApiAsync() {
        var services = new ServiceCollection().AddSingleton(_queue).BuildServiceProvider();
        var disabled = new ApiLogStorageOptions { Enabled = false, QueueKey = "test:logs" };
        var context = new DefaultHttpContext { RequestServices = services, Request = { Path = "/api/x" } };
        await new ApiHttpLoggingMiddleware(_ => Task.CompletedTask, NullLogger<ApiHttpLoggingMiddleware>.Instance, Options.Create(disabled))
            .InvokeAsync(context)
            .ConfigureAwait(true);
        var other = new DefaultHttpContext { RequestServices = services, Request = { Path = "/health" } };
        await new ApiHttpLoggingMiddleware(_ => Task.CompletedTask, NullLogger<ApiHttpLoggingMiddleware>.Instance, Options.Create(_options))
            .InvokeAsync(other)
            .ConfigureAwait(true);
        await Task.Delay(100).ConfigureAwait(true);

        Assert.Equal(0, _redis.Length("test:logs"));
    }

    /// <summary>
    ///     验证队列超过容量时丢弃最旧的日志
    /// </summary>
    [Fact]
    public async Task Queue_DropsOldestWhenOverCapacityAsync() {
        var small = new ApiLogStorageQueue(
            FakeListDatabase.CreateMultiplexer(_redis), new ApiLogStorageOptions { QueueKey = "test:small", QueueCapacity = 1 }
        );
        for (var i = 0; i < 1005; i++) {
            await small.EnqueueAsync(NewLog($"/api/{i}")).ConfigureAwait(true);
        }

        var all = await small.DequeueBatchAsync(5000).ConfigureAwait(true);

        Assert.Equal(1000, all.Count);
        Assert.Equal(ApiEndpointKey.ComputePathCrc32("/api/5"), all[0].PathCrc32);
        Assert.Equal(ApiEndpointKey.ComputePathCrc32("/api/1004"), all[^1].PathCrc32);
    }

    /// <summary>
    ///     验证入队后按顺序取出，取出数量受限，并保持字段完整
    /// </summary>
    [Fact]
    public async Task Queue_RoundTripsLogsInOrderAsync() {
        for (var i = 0; i < 5; i++) {
            await _queue.EnqueueAsync(NewLog($"/api/{i}")).ConfigureAwait(true);
        }

        var first = await _queue.DequeueBatchAsync(3).ConfigureAwait(true);
        var rest = await _queue.DequeueBatchAsync(10).ConfigureAwait(true);

        Assert.Equal(Crcs("/api/0", "/api/1", "/api/2"), first.Select(x => x.PathCrc32));
        Assert.Equal(Crcs("/api/3", "/api/4"), rest.Select(x => x.PathCrc32));
        Assert.Equal(1L, first[0].UserId);
        Assert.Empty(await _queue.DequeueBatchAsync(10).ConfigureAwait(true));
    }

    /// <summary>
    ///     验证放回队列的日志排在队首，顺序与原来一致
    /// </summary>
    [Fact]
    public async Task Requeue_PutsLogsBackAtHeadInOrderAsync() {
        await _queue.EnqueueAsync(NewLog("/api/new")).ConfigureAwait(true);

        await _queue.RequeueAsync([NewLog("/api/a"), NewLog("/api/b")]).ConfigureAwait(true);

        Assert.Equal(Crcs("/api/a", "/api/b", "/api/new"), (await _queue.DequeueBatchAsync(10).ConfigureAwait(true)).Select(x => x.PathCrc32));
    }

    /// <summary>
    ///     验证同一批日志重复写入时主键冲突的行被跳过，不抛异常也不产生重复行
    /// </summary>
    [Fact]
    public async Task Tables_InsertIsIdempotentForDuplicateIdsAsync() {
        var day = DateTime.UtcNow;
        var log = NewLog("/api/dup", day);

        _ = await _tables.InsertAsync([log], CancellationToken.None).ConfigureAwait(true);
        _ = await _tables.InsertAsync([log, NewLog("/api/new", day)], CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(2L, Count(ApiRequestLogTableService.GetTableName(day)));
    }

    /// <summary>
    ///     计算多个路径的 CRC32，顺序与参数一致
    /// </summary>
    /// <param name="paths">请求路径</param>
    /// <returns>路径 CRC32 列表</returns>
    private static int[] Crcs(params string[] paths) {
        return [.. paths.Select(ApiEndpointKey.ComputePathCrc32)];
    }

    /// <summary>
    ///     创建测试用日志
    /// </summary>
    /// <param name="path">请求路径</param>
    /// <param name="createdAt">创建时间，默认当前 UTC 时间</param>
    /// <returns>请求日志</returns>
    private static ApiRequestLog NewLog(
        string path
        , DateTime? createdAt = null
    ) {
        return new ApiRequestLog
        {
            CreatedAt = createdAt ?? DateTime.UtcNow
            , Method = ApiRequestMethod.Post
            , PathCrc32 = ApiEndpointKey.ComputePathCrc32(path)
            , StatusCode = 200
            , UserId = 1
            , TraceId = 7
            , WorkerId = 1
        };
    }

    /// <summary>
    ///     轮询等待条件成立，超时则返回，由后续断言报告失败
    /// </summary>
    /// <param name="condition">条件</param>
    /// <returns>异步任务</returns>
    private static async Task WaitUntilAsync(Func<bool> condition) {
        for (var i = 0; i < 100 && !condition(); i++) {
            await Task.Delay(30).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     统计指定分表的行数，表不存在时返回 0
    /// </summary>
    /// <param name="table">分表名称</param>
    /// <returns>行数</returns>
    private long Count(string table) {
        using var command = _keepAlive.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = @name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = table;
        _ = command.Parameters.Add(parameter);
        if ((long)command.ExecuteScalar()! == 0) {
            return 0;
        }

        using var count = _keepAlive.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)count.ExecuteScalar()!;
    }

    /// <summary>
    ///     只实现请求日志队列用到的 List 操作的内存假 Redis 数据库，其余调用返回默认值
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeListDatabase : DispatchProxy
    {
        private readonly Dictionary<string, List<string>> _lists = new(StringComparer.Ordinal);
        private readonly Lock _sync = new();

        /// <summary>
        ///     本代理作为 IDatabase 的视图，供连接复用器返回
        /// </summary>
        private IDatabase? Redis { get; set; }

        /// <summary>
        ///     创建假数据库
        /// </summary>
        /// <returns>假数据库代理</returns>
        public static FakeListDatabase Create() {
            // 代理的运行时类型继承 FakeListDatabase 并实现 IDatabase，先以 IDatabase 创建再保存这个接口视图，调用方不必再做类型转换
            var proxy = Create<IDatabase, FakeListDatabase>();

            // ReSharper disable once SuspiciousTypeConversion.Global
            var database = (FakeListDatabase)proxy;
            database.Redis = proxy;
            return database;
        }

        /// <summary>
        ///     创建只返回指定假数据库的连接复用器
        /// </summary>
        /// <param name="database">假数据库</param>
        /// <returns>连接复用器代理</returns>
        public static IConnectionMultiplexer CreateMultiplexer(FakeListDatabase database) {
            var multiplexer = Create<IConnectionMultiplexer, FakeMultiplexer>();

            // ReSharper disable once SuspiciousTypeConversion.Global
            ((FakeMultiplexer)multiplexer).Database = database.Redis;
            return multiplexer;
        }

        /// <summary>
        ///     读取指定列表的长度
        /// </summary>
        /// <param name="key">列表键</param>
        /// <returns>列表长度</returns>
        public int Length(string key) {
            lock (_sync) {
                return _lists.TryGetValue(key, out var list) ? list.Count : 0;
            }
        }

        /// <summary>
        ///     拦截数据库调用：脚本模拟 rpush 加 ltrim，ListLeftPop 取头部，ListLeftPush 放回头部
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            var name = targetMethod!.Name;
            object? result = null;
            lock (_sync) {
                switch (name) {
                    case "ScriptEvaluateAsync":
                    {
                        var key = ((RedisKey[])args![1]!)[0].ToString();
                        var values = (RedisValue[])args[2]!;
                        var list = GetList(key);
                        list.Add(values[0].ToString());
                        var capacity = (int)values[1];
                        if (list.Count > capacity) {
                            list.RemoveRange(0, list.Count - capacity);
                        }

                        result = RedisResult.Create(1);
                        break;
                    }

                    case "ListLeftPopAsync" when args![1] is long count:
                    {
                        var list = GetList(args[0]!.ToString()!);
                        var take = list.Take((int)count).Select(x => (RedisValue)x).ToArray();
                        list.RemoveRange(0, take.Length);
                        result = take;
                        break;
                    }

                    case "ListLeftPushAsync" when args![1] is RedisValue[] pushed:
                    {
                        var list = GetList(args[0]!.ToString()!);
                        foreach (var value in pushed) {
                            list.Insert(0, value.ToString());
                        }

                        result = (long)list.Count;
                        break;
                    }
                }
            }

            return ToReturnValue(targetMethod.ReturnType, result);
        }

        /// <summary>
        ///     按方法返回类型包装调用结果：Task 返回已完成任务，Task&lt;T&gt; 返回带结果的任务，其余返回结果或类型默认值
        /// </summary>
        /// <param name="type">被调用方法的返回类型</param>
        /// <param name="result">脚本计算出的结果，没有结果时为空</param>
        /// <returns>符合返回类型的调用结果</returns>
        private static object? ToReturnValue(
            Type type
            , object? result
        ) {
            if (type == typeof(Task)) {
                return Task.CompletedTask;
            }

            if (type is not { IsGenericType: true } || type.GetGenericTypeDefinition() != typeof(Task<>)) {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            var inner = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(inner)
                .Invoke(null, [result ?? (inner.IsValueType ? Activator.CreateInstance(inner) : null)]);
        }

        /// <summary>
        ///     获取或创建指定键的列表，调用方须已持有锁
        /// </summary>
        /// <param name="key">列表键</param>
        /// <returns>列表</returns>
        private List<string> GetList(string key) {
            if (_lists.TryGetValue(key, out var list)) {
                return list;
            }

            list = [];
            _lists[key] = list;

            return list;
        }
    }

    /// <summary>
    ///     只返回固定数据库的连接复用器代理
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeMultiplexer : DispatchProxy
    {
        /// <summary>
        ///     GetDatabase 返回的假数据库
        /// </summary>
        public IDatabase? Database { get; set; }

        /// <summary>
        ///     拦截连接复用器调用：GetDatabase 返回假数据库，其余返回默认值
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return targetMethod!.Name == nameof(IConnectionMultiplexer.GetDatabase)
                ? Database
                : targetMethod.ReturnType.IsValueType
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
        }
    }
}