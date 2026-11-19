using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证告警监控管理接口：保存时的来源、指标、类型、操作符和阈值组合校验，判定配置变化或停用时清除报警状态，批量删除，以及扫描接口的数据权限
/// </summary>
public sealed class AlertMonitorsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AlertMonitorsControllerTests" /> class.
    ///     创建内存数据库
    /// </summary>
    public AlertMonitorsControllerTests() {
        var connectionString = $"DataSource=file:alert-monitor-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using var init = NewDb(AllDataScope());
        _ = init.Database.EnsureCreated();
    }

    /// <summary>
    ///     批量删除：全部存在时删除，任一不存在时整批拒绝且一条都不删
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task DeleteRemovesAllOrNothingAsync() {
        var first = await SeedAlertingAsync().ConfigureAwait(true);
        var second = await SeedAlertingAsync().ConfigureAwait(true);

        _ = Assert.IsType<NotFoundObjectResult>(
            (await NewController(AllDataScope()).DeleteAsync(new DeleteAlertMonitorRequest { Ids = [first, 1] }).ConfigureAwait(true)).Result
        );
        await using (var db = NewDb(AllDataScope())) {
            Assert.Equal(2, await db.AlertMonitors.CountAsync().ConfigureAwait(true));
        }

        _ = Assert.IsType<OkObjectResult>(
            (await NewController(AllDataScope()).DeleteAsync(new DeleteAlertMonitorRequest { Ids = [first, second] }).ConfigureAwait(true)).Result
        );
        await using var check = NewDb(AllDataScope());
        Assert.Equal(0, await check.AlertMonitors.CountAsync().ConfigureAwait(true));
    }

    /// <summary>
    ///     停用时清除报警状态，避免再次启用时被旧状态影响；启用开关接口同样如此，且不影响仍启用的监控
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task DisablingClearsAlertStateAsync() {
        var id = await SeedAlertingAsync().ConfigureAwait(true);
        var other = await SeedAlertingAsync().ConfigureAwait(true);

        _ = await NewController(AllDataScope()).SetEnabledAsync(new SetEnabledRequest { Id = id, IsEnabled = false }).ConfigureAwait(true);
        Assert.False(await IsAlertingAsync(id).ConfigureAwait(true));
        Assert.True(await IsAlertingAsync(other).ConfigureAwait(true));

        _ = Assert.IsType<NotFoundObjectResult>(
            (await NewController(AllDataScope()).SetEnabledAsync(new SetEnabledRequest { Id = 1, IsEnabled = true }).ConfigureAwait(true)).Result
        );
    }

    /// <summary>
    ///     释放内存数据库连接
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
    }

    /// <summary>
    ///     非法组合被拒绝且不落库：字典来源缺目录编码、字典来源用长度、长度按字符串比较、操作符与类型不匹配、数字阈值不是数字、字符串阈值为空、名称或目标为空
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task InvalidCombinationsAreRejectedAndNothingIsSavedAsync() {
        RequestSpec[] rejected =
        [
            Valid(x => x with { CategoryCode = " " }), Valid(x => x with { Metric = AlertMonitorMetric.Length })
            , Valid(x => x with
                {
                    Source = AlertMonitorSource.Redis
                    , Metric = AlertMonitorMetric.Length
                    , ValueType = AlertMonitorValueType.Text
                    , Operator = AlertMonitorOperator.Equal
                    , Threshold = "x"
                }
            )
            , Valid(x => x with { ValueType = AlertMonitorValueType.Number, Operator = AlertMonitorOperator.Contains })
            , Valid(x => x with { ValueType = AlertMonitorValueType.Text, Operator = AlertMonitorOperator.GreaterThan, Threshold = "x" })
            , Valid(x => x with { Threshold = "abc" })
            , Valid(x => x with { ValueType = AlertMonitorValueType.Text, Operator = AlertMonitorOperator.Contains, Threshold = string.Empty })
            , Valid(x => x with { Name = " " }), Valid(x => x with { Target = " " }), Valid(x => x with { Operator = (AlertMonitorOperator)99 })
        ];
        foreach (var request in rejected) {
            _ = Assert.IsType<BadRequestObjectResult>(
                (await NewController(AllDataScope()).CreateAsync(request.ToRequest()).ConfigureAwait(true)).Result
            );
        }

        await using var db = NewDb(AllDataScope());
        Assert.Equal(0, await db.AlertMonitors.CountAsync().ConfigureAwait(true));
    }

    /// <summary>
    ///     修改了判定配置（阈值、操作符、目标等）时清除报警状态让新配置重新判定；只改名称、备注、提醒用户时保留报警状态
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task JudgementChangesClearAlertStateButCosmeticChangesKeepItAsync() {
        var id = await SeedAlertingAsync().ConfigureAwait(true);

        var cosmetic = Valid(x => x with { Id = id, Name = "renamed", Remark = "note", Mentions = "carol" });
        _ = await NewController(AllDataScope()).UpdateAsync(cosmetic.ToRequest()).ConfigureAwait(true);
        Assert.True(await IsAlertingAsync(id).ConfigureAwait(true));

        var judgement = Valid(x => x with { Id = id, Threshold = "99" });
        _ = await NewController(AllDataScope()).UpdateAsync(judgement.ToRequest()).ConfigureAwait(true);
        Assert.False(await IsAlertingAsync(id).ConfigureAwait(true));
    }

    /// <summary>
    ///     扫描字典来源的监控：字典项正常时不报警，字典项被停用或被删除视为取不到值而报警，状态变化后写回最近取值和说明
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ScanReadsDictionaryItemsAndTreatsDisabledOrMissingAsBreachAsync() {
        await using (var seed = NewDb(AllDataScope())) {
            var category = new DictionaryCategory { Code = "scan_cat", Name = "scan" };
            _ = seed.DictionaryCategories.Add(category);
            _ = seed.DictionaryItems.Add(new DictionaryItem { Category = category, CategoryId = category.Id, Label = "limit", Value = "10" });
            _ = seed.AlertMonitors.Add(Monitor("normal", "scan_cat", "limit", AlertMonitorOperator.GreaterThan, "50"));
            _ = seed.AlertMonitors.Add(Monitor("missing", "scan_cat", "gone", AlertMonitorOperator.GreaterThan, "50"));
            _ = seed.AlertMonitors.Add(Monitor("off", "scan_cat", "limit", AlertMonitorOperator.GreaterThan, "5", false));
            _ = await seed.SaveChangesAsync().ConfigureAwait(true);
        }

        var result = (await ScanAsync().ConfigureAwait(true)).Data!;
        Assert.Equal((2, 1, 0, 1), (result.Scanned, result.Raised, result.Resolved, result.Active));

        await using var db = NewDb(AllDataScope());
        var rows = await db.AlertMonitors.AsNoTracking().ToDictionaryAsync(x => x.Name).ConfigureAwait(true);
        Assert.Equal((false, "10", "Normal"), (rows["normal"].AlertActive, rows["normal"].LastValue, rows["normal"].LastMessage));
        Assert.Equal((true, "Dictionary item does not exist"), (rows["missing"].AlertActive, rows["missing"].LastMessage));
        Assert.Null(rows["off"].LastCheckedAt);

        // 字典项恢复后再扫描：missing 仍缺失保持报警，不重复触发
        var again = (await ScanAsync().ConfigureAwait(true)).Data!;
        Assert.Equal((2, 0, 0, 1), (again.Scanned, again.Raised, again.Resolved, again.Active));
    }

    /// <summary>
    ///     扫描接口只允许全部数据权限调用
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ScanRequiresAllDataPermissionAsync() {
        var limited = new DataScopeContext();
        limited.Initialize(2, false, false, new HashSet<long>(), 100);

        _ = await Assert.ThrowsAsync<DataAccessDeniedException>(() => NewController(limited).ScanAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>
    ///     合法的 Redis 长度监控能保存，提醒用户被规范化，字典来源的目录编码被保留而 Redis 来源的被清空
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ValidMonitorsAreSavedWithNormalizedFieldsAsync() {
        var redis = Valid(x => x with
            {
                Source = AlertMonitorSource.Redis
                , CategoryCode = "ignored"
                , Target = " queue:report "
                , Metric = AlertMonitorMetric.Length
                , Threshold = "1000"
                , Mentions = "alice, @Bob"
            }
        );
        var created = await NewController(AllDataScope()).CreateAsync(redis.ToRequest()).ConfigureAwait(true);
        var item = Assert.IsType<ApiResponse<AlertMonitorListItem>>(((OkObjectResult)created.Result!).Value).Data!;

        Assert.Equal(
            ("queue:report", string.Empty, AlertMonitorMetric.Length, "@alice @Bob"), (item.Target, item.CategoryCode, item.Metric, item.Mentions)
        );

        var dictionary = Valid(x => x with { CategoryCode = " system_settings ", Target = "Maximum export rows", Threshold = "5" });
        var second = await NewController(AllDataScope()).CreateAsync(dictionary.ToRequest()).ConfigureAwait(true);
        Assert.Equal("system_settings", Assert.IsType<ApiResponse<AlertMonitorListItem>>(((OkObjectResult)second.Result!).Value).Data!.CategoryCode);
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
    ///     创建启用的字典来源监控
    /// </summary>
    /// <param name="name">监控名称</param>
    /// <param name="category">字典目录编码</param>
    /// <param name="label">字典标签</param>
    /// <param name="op">操作符</param>
    /// <param name="threshold">阈值</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>告警监控</returns>
    private static AlertMonitor Monitor(
        string name
        , string category
        , string label
        , AlertMonitorOperator op
        , string threshold
        , bool enabled = true
    ) {
        return new AlertMonitor
        {
            Name = name
            , Source = AlertMonitorSource.Dictionary
            , CategoryCode = category
            , Target = label
            , ValueType = AlertMonitorValueType.Number
            , Operator = op
            , Threshold = threshold
            , IsEnabled = enabled
        };
    }

    /// <summary>
    ///     基于合法的默认请求做修改，用于构造各种非法或变更场景
    /// </summary>
    /// <param name="change">修改委托</param>
    /// <returns>修改后的请求描述</returns>
    private static RequestSpec Valid(Func<RequestSpec, RequestSpec> change) {
        return change(
            new RequestSpec(
                null, "m", AlertMonitorSource.Dictionary, "cat", "label", AlertMonitorMetric.Value, AlertMonitorValueType.Number
                , AlertMonitorOperator.GreaterThan, "10", string.Empty, string.Empty
            )
        );
    }

    /// <summary>
    ///     读取监控是否处于报警中
    /// </summary>
    /// <param name="id">监控主键</param>
    /// <returns>处于报警中返回 true</returns>
    private async Task<bool> IsAlertingAsync(long id) {
        await using var db = NewDb(AllDataScope());
        return await db.AlertMonitors.AsNoTracking().Where(x => x.Id == id).Select(x => x.AlertActive).SingleAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     创建告警监控控制器，扫描服务使用假 Redis 和不发送消息的报警服务
    /// </summary>
    /// <param name="scope">数据权限上下文</param>
    /// <returns>告警监控控制器</returns>
    private AlertMonitorsController NewController(DataScopeContext scope) {
        var db = NewDb(scope);
        var redis = FakeRedis.Create();
        var services = new ServiceCollection();
        _ = services
            .AddHttpClient(ExternalHttpRequestService.CLIENT_NAME)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var http = new ExternalHttpRequestService(
            services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>(), new HttpContextAccessor()
            , NullLogger<ExternalHttpRequestService>.Instance
        );

        // 报警机器人未配置：报警服务只记录日志并跳过发送，扫描结果和状态写回不受影响
        var alerts = new ScheduledJobAlertService(
            new DictionarySnapshotService(db, redis, NullLogger<DictionarySnapshotService>.Instance), http
            , NullLogger<ScheduledJobAlertService>.Instance
        );
        return new AlertMonitorsController(db, new AlertMonitorScanService(db, redis, alerts));
    }

    /// <summary>
    ///     创建绑定内存数据库的应用数据库上下文
    /// </summary>
    /// <param name="scope">数据权限上下文</param>
    /// <returns>应用数据库上下文</returns>
    private AppDbContext NewDb(DataScopeContext scope) {
        return new AppDbContext(_options, scope, new DataScopeCache(FakeRedis.Create()));
    }

    /// <summary>
    ///     通过控制器执行一次扫描
    /// </summary>
    /// <returns>扫描响应</returns>
    private async Task<ApiResponse<AlertMonitorScanResult>> ScanAsync() {
        var response = await NewController(AllDataScope()).ScanAsync(CancellationToken.None).ConfigureAwait(false);
        return Assert.IsType<ApiResponse<AlertMonitorScanResult>>(((OkObjectResult)response.Result!).Value);
    }

    /// <summary>
    ///     把一个监控状态置为报警中并返回主键，请求描述与 <see cref="Valid" /> 的默认值一致，便于做“只改某一项”的对比
    /// </summary>
    /// <returns>监控主键</returns>
    private async Task<long> SeedAlertingAsync() {
        await using var db = NewDb(AllDataScope());
        var monitor = new AlertMonitor
        {
            Name = "m"
            , Source = AlertMonitorSource.Dictionary
            , CategoryCode = "cat"
            , Target = "label"
            , ValueType = AlertMonitorValueType.Number
            , Operator = AlertMonitorOperator.GreaterThan
            , Threshold = "10"
            , AlertActive = true
        };
        _ = db.AlertMonitors.Add(monitor);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return monitor.Id;
    }

    /// <summary>
    ///     保存请求的不可变描述，便于用 with 表达式构造变体
    /// </summary>
    /// <param name="Id">监控主键</param>
    /// <param name="Name">名称</param>
    /// <param name="Source">来源</param>
    /// <param name="CategoryCode">字典目录编码</param>
    /// <param name="Target">目标</param>
    /// <param name="Metric">指标</param>
    /// <param name="ValueType">比较类型</param>
    /// <param name="Operator">操作符</param>
    /// <param name="Threshold">阈值</param>
    /// <param name="Mentions">提醒用户</param>
    /// <param name="Remark">备注</param>
    private sealed record RequestSpec(
        long? Id
        , string Name
        , AlertMonitorSource Source
        , string CategoryCode
        , string Target
        , AlertMonitorMetric Metric
        , AlertMonitorValueType ValueType
        , AlertMonitorOperator Operator
        , string Threshold
        , string Mentions
        , string Remark)
    {
        /// <summary>
        ///     转换为接口请求
        /// </summary>
        /// <returns>保存请求</returns>
        public SaveAlertMonitorRequest ToRequest() {
            return new SaveAlertMonitorRequest
            {
                Id = Id
                , Name = Name
                , Source = Source
                , CategoryCode = CategoryCode
                , Target = Target
                , Metric = Metric
                , ValueType = ValueType
                , Operator = Operator
                , Threshold = Threshold
                , Mentions = Mentions
                , Remark = Remark
            };
        }
    }

    /// <summary>
    ///     假数据库：异步方法返回已完成的默认值任务
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeDatabase : DispatchProxy
    {
        /// <summary>
        ///     拦截数据库调用，异步方法返回已完成的任务
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>已完成的任务或默认值</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            var type = targetMethod?.ReturnType;
            if (type == typeof(Task)) {
                return Task.CompletedTask;
            }

            if (type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(Task<>)) {
                var inner = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(inner)
                    .Invoke(null, [inner.IsValueType ? Activator.CreateInstance(inner) : null]);
            }

            return type?.IsValueType == true ? Activator.CreateInstance(type) : null;
        }
    }

    /// <summary>
    ///     最小化的假 Redis 连接：GetDatabase 返回对任意调用给出已完成默认值的数据库代理，键类型一律为 None
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeRedis : DispatchProxy
    {
        /// <summary>
        ///     创建假的 Redis 连接复用器
        /// </summary>
        /// <returns>假连接复用器</returns>
        public static IConnectionMultiplexer Create() {
            return Create<IConnectionMultiplexer, FakeRedis>();
        }

        /// <summary>
        ///     拦截接口调用：GetDatabase 返回假数据库，其余调用返回默认值
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase)) {
                return Create<IDatabase, FakeDatabase>();
            }

            return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }
}