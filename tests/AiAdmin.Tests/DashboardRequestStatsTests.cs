using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证仪表盘操作日志统计：今日与昨日按本地小时对比、接口占比取前若干并合并其余、时区对齐、权限与空数据
/// </summary>
public sealed class DashboardRequestStatsTests : IAsyncLifetime, IDisposable
{
    private const int _OFFSET_MINUTES = 480;
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ApiRequestLogTableService _tables;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DashboardRequestStatsTests" /> class.
    ///     初始化主库、分表服务和接口表
    /// </summary>
    public DashboardRequestStatsTests() {
        var connectionString = $"DataSource=file:dashboard-stats-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        _tables = new ApiRequestLogTableService("sqlite", connectionString);
        using var db = NewDb(true);
        _ = db.Database.EnsureCreated();
        _ = db.ApiEndpoints.Add(
            new ApiEndpoint
            {
                Name = "User list", Method = "POST", Path = "/api/user/list", PathCrc32 = ApiEndpointKey.ComputePathCrc32("/api/user/list")
            }
        );
        _ = db.SaveChanges();
    }

    /// <summary>
    ///     释放服务和数据库连接
    /// </summary>
    public void Dispose() {
        _tables.Dispose();
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     无需额外异步清理，资源由 Dispose 释放
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task DisposeAsync() {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     无需异步初始化，数据由各测试自行写入
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task InitializeAsync() {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     验证刷新接口只允许全部数据权限调用，刷新后写入预热时区的缓存，读取只命中缓存而不重新扫表；缓存缺失时返回空结果
    /// </summary>
    [Fact]
    public async Task RefreshRequestStats_WarmsCacheAndRequiresAllDataAsync() {
        _ = await Assert.ThrowsAsync<DataAccessDeniedException>(() => NewController(false).RefreshRequestStatsAsync()).ConfigureAwait(true);

        var todayLocal = LocalToday();
        await InsertAsync(Local(todayLocal, 9, 0), "/api/user/list", 3).ConfigureAwait(true);
        var controller = NewController(true);
        var refresh = await controller.RefreshRequestStatsAsync().ConfigureAwait(true);
        var refreshed = Assert.IsType<ApiResponse<RefreshDashboardRequestStatsResult>>(Assert.IsType<OkObjectResult>(refresh.Result).Value, false);
        Assert.Equal(1, refreshed.Data!.Refreshed);

        // 刷新后再写入日志，缓存永不过期且读取不会重新扫表，读到的仍是刷新时的结果
        await InsertAsync(Local(todayLocal, 9, 30), "/api/user/list", 4).ConfigureAwait(true);
        var cached = await controller.RequestStatsAsync().ConfigureAwait(true);
        var stats = Assert.IsType<ApiResponse<DashboardRequestStatsResult>>(Assert.IsType<OkObjectResult>(cached.Result).Value, false).Data!;
        Assert.Equal(3, stats.TodayTotal);
    }

    /// <summary>
    ///     验证今日与昨日按本地小时对齐：东八区本地 9 点是 UTC 1 点，今天昨天各有数据，其余时段为 0
    /// </summary>
    [Fact]
    public async Task RequestStats_AlignsHourlyTrendToLocalTimeAsync() {
        var todayLocal = LocalToday();
        await InsertAsync(Local(todayLocal, 9, 5), "/api/user/list", 3).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal, 9, 50), "/api/role/list", 2).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-1), 9, 30), "/api/user/list", 4).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-1), 23, 59), "/api/user/list", 1).ConfigureAwait(true);

        var stats = await StatsAsync().ConfigureAwait(true);

        Assert.Equal(24, stats.HourlyTrend.Count);
        Assert.Equal(["00", "01"], stats.HourlyTrend.Take(2).Select(x => x.Date));
        Assert.Equal((5, 4), (stats.HourlyTrend[9].Primary, stats.HourlyTrend[9].Secondary));
        Assert.Equal((0, 1), (stats.HourlyTrend[23].Primary, stats.HourlyTrend[23].Secondary));
        Assert.Equal(0, stats.HourlyTrend[8].Primary + stats.HourlyTrend[8].Secondary);
        Assert.Equal(5, stats.TodayTotal);
        Assert.Equal(5, stats.YesterdayTotal);
    }

    /// <summary>
    ///     验证接口占比只统计今日、按请求数倒序、还原路径；超过 8 个时其余合并为名称为空的一项
    /// </summary>
    [Fact]
    public async Task RequestStats_ApiDistributionIsTodayOnlyTopNWithOthersAsync() {
        var todayLocal = LocalToday();
        await InsertAsync(Local(todayLocal, 10, 0), "/api/user/list", 5).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-1), 10, 0), "/api/user/list", 50).ConfigureAwait(true);
        for (var i = 1; i <= 9; i++) {
            await InsertAsync(Local(todayLocal, 11, 0), $"/api/other/{i}", i).ConfigureAwait(true);
        }

        var stats = await StatsAsync().ConfigureAwait(true);

        // 共 10 个接口（user/list 加 other/1 到 9），只列前 8 个，其余合并为一项，所以共 9 项；按请求数倒序
        Assert.Equal(9, stats.ApiDistribution.Count);
        Assert.Equal([9, 8, 7, 6, 5, 5, 4, 3], stats.ApiDistribution.Take(8).Select(x => x.Value));
        Assert.Equal(ApiEndpointKey.ComputePathCrc32("/api/other/9").ToString(CultureInfo.InvariantCulture), stats.ApiDistribution[0].Path);

        // 接口表里注册过的接口同时还原路径和英文说明，说明交给前端按语言翻译
        Assert.Contains(
            stats.ApiDistribution
            , x => x is { Path: "/api/user/list", Name: "User list", Value: 5 } && x.PathCrc32 == ApiEndpointKey.ComputePathCrc32("/api/user/list")
        );

        // 其余合并项路径和说明都为空，数量是被挤出前 8 的两个接口（2 和 1）之和
        Assert.Equal(
            (string.Empty, string.Empty, 3), (stats.ApiDistribution[^1].Path, stats.ApiDistribution[^1].Name, stats.ApiDistribution[^1].Value)
        );
        Assert.Equal(stats.TodayTotal, stats.ApiDistribution.Sum(x => x.Value));
    }

    /// <summary>
    ///     验证本地早晨 0 点到 8 点的日志计入“今天”：这段时间对应 UTC 的前一天，若错把 UTC 零点当作今天起点就会被算成昨天，
    ///     测试不依赖运行时刻，数据总是放在本地今天 3 点和本地昨天 3 点
    /// </summary>
    [Fact]
    public async Task RequestStats_CountsLocalEarlyMorningAsTodayAsync() {
        var todayLocal = LocalToday();
        await InsertAsync(Local(todayLocal, 3, 0), "/api/user/list", 4).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-1), 3, 0), "/api/user/list", 2).ConfigureAwait(true);

        var stats = await StatsAsync().ConfigureAwait(true);

        Assert.Equal((4, 2), (stats.HourlyTrend[3].Primary, stats.HourlyTrend[3].Secondary));
        Assert.Equal((4, 2), (stats.TodayTotal, stats.YesterdayTotal));
        Assert.Equal(4, Assert.Single(stats.ApiDistribution).Value);
    }

    /// <summary>
    ///     验证接口表里找不到的路径使用 CRC32 数值作名称，没有任何日志时返回 24 个零点位和空分布，没有全部数据权限时拒绝
    /// </summary>
    [Fact]
    public async Task RequestStats_HandlesUnknownApiEmptyDataAndPermissionAsync() {
        var empty = await StatsAsync().ConfigureAwait(true);
        Assert.Equal(24, empty.HourlyTrend.Count);
        Assert.All(empty.HourlyTrend, x => Assert.Equal((0, 0), (x.Primary, x.Secondary)));
        Assert.Empty(empty.ApiDistribution);
        Assert.Equal(0, empty.TodayTotal);

        await InsertAsync(Local(LocalToday(), 10, 0), "/api/not/in/table", 2).ConfigureAwait(true);
        var unknown = await StatsAsync().ConfigureAwait(true);
        var share = Assert.Single(unknown.ApiDistribution);
        Assert.Equal(ApiEndpointKey.ComputePathCrc32("/api/not/in/table").ToString(CultureInfo.InvariantCulture), share.Path);
        Assert.Equal(string.Empty, share.Name);

        _ = await Assert.ThrowsAsync<DataAccessDeniedException>(() => NewController(false).RequestStatsAsync()).ConfigureAwait(true);
    }

    /// <summary>
    ///     验证本地午夜边界：本地 0 点属于新的一天，本地前一天 23:59 属于昨天；更早和明天的日志不计入
    /// </summary>
    [Fact]
    public async Task RequestStats_UsesLocalMidnightBoundariesAsync() {
        var todayLocal = LocalToday();
        await InsertAsync(Local(todayLocal, 0, 0), "/api/user/list", 1).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-1), 23, 59), "/api/user/list", 1).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(-2), 23, 59), "/api/user/list", 7).ConfigureAwait(true);
        await InsertAsync(Local(todayLocal.AddDays(1), 0, 0), "/api/user/list", 9).ConfigureAwait(true);

        var stats = await StatsAsync().ConfigureAwait(true);

        Assert.Equal(1, stats.TodayTotal);
        Assert.Equal(1, stats.YesterdayTotal);
        Assert.Equal(1, stats.HourlyTrend[0].Primary);
        Assert.Equal(1, stats.HourlyTrend[23].Secondary);
    }

    /// <summary>
    ///     把本地日期加时分换算成 UTC 时刻
    /// </summary>
    /// <param name="localDay">本地日期零点</param>
    /// <param name="hour">本地小时</param>
    /// <param name="minute">本地分钟</param>
    /// <returns>UTC 时间</returns>
    private static DateTime Local(
        DateTime localDay
        , int hour
        , int minute
    ) {
        return DateTime.SpecifyKind(localDay.AddHours(hour).AddMinutes(minute).AddMinutes(-_OFFSET_MINUTES), DateTimeKind.Utc);
    }

    /// <summary>
    ///     客户端时区下今天的本地零点（本地时间，Kind 不指定）
    /// </summary>
    /// <returns>本地今天零点</returns>
    private static DateTime LocalToday() {
        return DateTime.UtcNow.AddMinutes(_OFFSET_MINUTES).Date;
    }

    /// <summary>
    ///     创建指定数量的同路径请求日志
    /// </summary>
    /// <param name="createdAt">创建时间</param>
    /// <param name="path">请求路径</param>
    /// <param name="count">日志数量</param>
    /// <returns>日志集合</returns>
    private static List<ApiRequestLog> Logs(
        DateTime createdAt
        , string path
        , int count
    ) {
        return
        [
            .. Enumerable
                .Range(0, count)
                .Select(_ => new ApiRequestLog
                    {
                        CreatedAt = createdAt
                        , Method = ApiRequestMethod.Post
                        , PathCrc32 = ApiEndpointKey.ComputePathCrc32(path)
                        , StatusCode = 200
                        , TraceId = 1
                        , WorkerId = 1
                    }
                )
        ];
    }

    /// <summary>
    ///     写入指定数量的请求日志
    /// </summary>
    /// <param name="createdAt">创建时间</param>
    /// <param name="path">请求路径</param>
    /// <param name="count">日志数量</param>
    /// <returns>异步任务</returns>
    private async Task InsertAsync(
        DateTime createdAt
        , string path
        , int count
    ) {
        _ = await _tables.InsertAsync(Logs(createdAt, path, count), CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     创建仪表盘控制器
    /// </summary>
    /// <param name="allData">是否拥有全部数据权限</param>
    /// <returns>仪表盘控制器</returns>
    private DashboardController NewController(bool allData) {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test"));
        var services = new ServiceCollection();
        _ = services.AddScoped(_ => NewDb(true));
        _ = services.AddSingleton(_tables);
        _ = services.AddScoped<DashboardRequestStatsService>();
        var provider = services.BuildServiceProvider();
        var cache = new DashboardRequestStatsCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), provider.GetRequiredService<IServiceScopeFactory>()
        );
        return new DashboardController(NewDb(allData), null!, cache)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    /// <summary>
    ///     创建主库上下文
    /// </summary>
    /// <param name="allData">是否拥有全部数据权限</param>
    /// <returns>应用数据库上下文</returns>
    private AppDbContext NewDb(bool allData) {
        var scope = new DataScopeContext();
        scope.Initialize(1, allData, false, new HashSet<long>(), 100);
        return new AppDbContext(_options, scope, null!);
    }

    /// <summary>
    ///     先调用刷新接口写入缓存（模拟计划作业），再调用统计接口读取并取出结果
    /// </summary>
    /// <returns>统计结果</returns>
    private async Task<DashboardRequestStatsResult> StatsAsync() {
        var controller = NewController(true);
        _ = await controller.RefreshRequestStatsAsync().ConfigureAwait(false);
        var result = await controller.RequestStatsAsync().ConfigureAwait(false);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<DashboardRequestStatsResult>>(ok.Value, false).Data!;
    }
}