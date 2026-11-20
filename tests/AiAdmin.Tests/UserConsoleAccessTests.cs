using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证普通用户没有系统总览的菜单和接口权限，只拥有常用功能和个人中心菜单，并且已有数据库启动后会撤销旧授权
/// </summary>
public sealed class UserConsoleAccessTests : IAsyncLifetime, IDisposable
{
    private readonly string _connectionString = $"DataSource=file:console-access-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection? _keepAlive;
    private ServiceProvider? _services;

    /// <summary>
    ///     验证基础接口表不再包含系统总览接口，并且系统总览接口表只有这两个接口
    /// </summary>
    [Fact]
    public void BasicApiKeys_DoNotIncludeDashboardOverviewEndpoints() {
        Assert.DoesNotContain(DatabaseInitializer.BasicApiKeys, x => x.Contains("api/dashboard", StringComparison.Ordinal));
        Assert.Equal(["GET /api/dashboard/overview", "GET /api/dashboard/system-info"], DatabaseInitializer.DashboardApiKeys);
    }

    /// <summary>
    ///     同步释放兜底：异步清理已释放的资源重复释放是安全的
    /// </summary>
    public void Dispose() {
        _keepAlive?.Dispose();
    }

    /// <summary>
    ///     释放内存数据库连接和服务容器
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        if (_services is not null) {
            await _services.DisposeAsync().ConfigureAwait(true);
        }

        if (_keepAlive is not null) {
            await _keepAlive.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    ///     验证已有数据库启动后：此前授予普通用户的系统总览菜单和接口被撤销，个人中心和常用功能菜单保留，管理员的接口授权保持不变
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ExistingDatabase_RevokesUserConsoleMenuAndApisAsync() {
        await DatabaseInitializer.InitializeSystemDataAsync(_services!).ConfigureAwait(true);
        await using (var scope = _services!.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await GrantLegacyAsync(db).ConfigureAwait(true);
        }

        // 模拟升级后重启：菜单种子逻辑与接口授权逻辑再运行一次
        await DatabaseInitializer.InitializeSystemDataAsync(_services!).ConfigureAwait(true);
        await DatabaseInitializer.InitializeRoleApisAsync(_services!).ConfigureAwait(true);

        var userMenus = await LoadMenuNamesAsync("R_USER").ConfigureAwait(true);
        Assert.DoesNotContain("Console", userMenus);
        Assert.Contains("QuickTiles", userMenus);
        Assert.Contains("UserCenter", userMenus);
        var userApis = await LoadApiKeysAsync("R_USER").ConfigureAwait(true);
        Assert.DoesNotContain(userApis, x => x.Contains("api/dashboard", StringComparison.Ordinal));
        var adminApis = await LoadApiKeysAsync("R_ADMIN").ConfigureAwait(true);
        Assert.Contains("GET /api/dashboard/overview", adminApis);
    }

    /// <summary>
    ///     创建内存数据库并注册初始化所需服务
    /// </summary>
    /// <returns>异步初始化任务</returns>
    public async Task InitializeAsync() {
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync().ConfigureAwait(true);
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        _ = services.AddSingleton(NoopRedis.Create());
        _ = services.AddSingleton<DataScopeCache>();
        _ = services.AddScoped(_ => AllDataScope());
        _ = services.AddScoped<ApiPermissionCache>();
        _ = services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connectionString));
        _services = services.BuildServiceProvider();
    }

    /// <summary>
    ///     验证新建数据库种子：普通用户有常用功能、个人中心及其目录，没有系统总览；管理员和超级管理员不受影响
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task NewDatabase_GrantsUserTilesAndUserCenterButNotConsoleAsync() {
        await DatabaseInitializer.InitializeSystemDataAsync(_services!).ConfigureAwait(true);

        var userMenus = await LoadMenuNamesAsync("R_USER").ConfigureAwait(true);
        Assert.Contains("QuickTiles", userMenus);
        Assert.Contains("Dashboard", userMenus);
        Assert.Contains("UserCenter", userMenus);
        Assert.Contains("SystemManagement", userMenus);
        Assert.DoesNotContain("Console", userMenus);
        Assert.DoesNotContain("BusinessConsole", userMenus);
    }

    /// <summary>
    ///     创建拥有全部数据权限的数据权限上下文
    /// </summary>
    /// <returns>数据权限上下文</returns>
    private static DataScopeContext AllDataScope() {
        var scope = new DataScopeContext();
        scope.Initialize(1, true, false, new HashSet<long>(), 100);
        return scope;
    }

    /// <summary>
    ///     把系统总览菜单和接口授权给普通用户，并给管理员授权总览接口，模拟升级前的数据库状态
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步写入任务</returns>
    private static async Task GrantLegacyAsync(AppDbContext db) {
        var userRole = await db.Roles.Include(x => x.RoleMenus).Include(x => x.RoleApis).SingleAsync(x => x.Code == "R_USER").ConfigureAwait(true);
        var adminRole = await db.Roles.Include(x => x.RoleApis).SingleAsync(x => x.Code == "R_ADMIN").ConfigureAwait(true);
        var console = await db.Menus.SingleAsync(x => x.Name == "Console").ConfigureAwait(true);
        userRole.RoleMenus.Add(new RoleMenu { Role = userRole, Menu = console });
        foreach (var key in DatabaseInitializer.DashboardApiKeys) {
            var parts = key.Split(' ', 2);
            var endpoint = new ApiEndpoint
            {
                Method = parts[0]
                , Path = parts[1].TrimStart('/')
                , Name = key
                , Controller = "Dashboard"
                , Action = key
                , ControllerName = "Dashboard"
            };
            _ = db.ApiEndpoints.Add(endpoint);
            userRole.RoleApis.Add(new RoleApi { Role = userRole, ApiEndpoint = endpoint });
            adminRole.RoleApis.Add(new RoleApi { Role = adminRole, ApiEndpoint = endpoint });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     读取角色已授权的接口键
    /// </summary>
    /// <param name="roleCode">角色编码</param>
    /// <returns>接口键集合</returns>
    private async Task<List<string>> LoadApiKeysAsync(string roleCode) {
        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db
            .RoleApis.Where(x => x.Role.Code == roleCode)
            .Select(x => new { x.ApiEndpoint.Method, x.ApiEndpoint.Path })
            .ToListAsync()
            .ConfigureAwait(true);
        return [.. rows.Select(x => ApiEndpointKey.Create(x.Method, x.Path))];
    }

    /// <summary>
    ///     读取角色已授权的菜单名称
    /// </summary>
    /// <param name="roleCode">角色编码</param>
    /// <returns>菜单名称集合</returns>
    private async Task<List<string>> LoadMenuNamesAsync(string roleCode) {
        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RoleMenus.Where(x => x.Role.Code == roleCode).Select(x => x.Menu.Name).ToListAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     最小的 Redis 数据库替身：异步方法返回已完成的任务，其余返回默认值，供数据权限缓存失效调用使用
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class NoopDatabase : DispatchProxy
    {
        /// <summary>
        ///     拦截数据库调用，异步方法返回已完成的任务，值类型返回默认值
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
    ///     最小的 Redis 连接复用器替身：GetDatabase 返回数据库替身，其余调用返回默认值
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class NoopRedis : DispatchProxy
    {
        /// <summary>
        ///     创建替身连接复用器
        /// </summary>
        /// <returns>替身连接复用器</returns>
        public static IConnectionMultiplexer Create() {
            return Create<IConnectionMultiplexer, NoopRedis>();
        }

        /// <summary>
        ///     拦截接口调用，GetDatabase 返回数据库替身，值类型返回默认值，其余返回 null
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase)) {
                return Create<IDatabase, NoopDatabase>();
            }

            var type = targetMethod?.ReturnType;
            return type?.IsValueType == true ? Activator.CreateInstance(type) : null;
        }
    }
}