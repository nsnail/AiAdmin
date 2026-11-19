using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;
using Menu = AiAdmin.Api.Models.Menu;
using Role = AiAdmin.Api.Models.Role;

namespace AiAdmin.Tests;

/// <summary>
///     验证失效中间表映射清理：只删除关联对象已不存在的映射，保留正常映射，并要求拥有全部数据权限
/// </summary>
public sealed class OrphanMappingCleanupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrphanMappingCleanupTests" /> class.
    ///     创建内存数据库，关闭外键约束以便写入历史遗留的孤儿映射
    /// </summary>
    public OrphanMappingCleanupTests() {
        var connectionString = $"DataSource=file:orphan-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using var init = NewDb(AllDataScope());
        _ = init.Database.EnsureCreated();
    }

    /// <summary>
    ///     验证批量设置报警：配置整体覆盖到所选作业，未选中的作业不受影响，提醒用户名被规范化，关闭报警时清除报警状态，开启时保留原有报警状态
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task BatchAlert_OverwritesSelectedJobsAndClearsStateWhenDisabledAsync() {
        var ids = new List<long>();
        await using (var seed = NewDb(AllDataScope())) {
            for (var i = 0; i < 3; i++) {
                var job = new ScheduledJob
                {
                    Name = $"batch-alert-{i}"
                    , CronExpression = "* * * * *"
                    , RequestUrl = "http://localhost/"
                    , AlertEnabled = true
                    , AlertActive = true
                    , AlertOverSeconds = 7
                };
                ids.Add(job.Id);
                _ = seed.ScheduledJobs.Add(job);
            }

            _ = await seed.SaveChangesAsync().ConfigureAwait(true);
        }

        var controller = NewController(AllDataScope());
        var enable = new BatchUpdateScheduledJobAlertRequest
        {
            Ids = [ids[0], ids[1]]
            , AlertEnabled = true
            , AlertOnFailure = false
            , AlertOverSeconds = 30
            , AlertMentions = "alice, @Bob"
        };
        var response = await controller.BatchUpdateAlertAsync(enable).ConfigureAwait(true);
        var body = Assert.IsType<ApiResponse<object>>(((OkObjectResult)response.Result!).Value);
        Assert.Contains("affected = 2", body.Data!.ToString(), StringComparison.Ordinal);

        await using (var check = NewDb(AllDataScope())) {
            var first = await check.ScheduledJobs.SingleAsync(x => x.Id == ids[0]).ConfigureAwait(true);
            Assert.Equal(
                (true, false, 30, "@alice @Bob", true)
                , (first.AlertEnabled, first.AlertOnFailure, first.AlertOverSeconds, first.AlertMentions, first.AlertActive)
            );
            var untouched = await check.ScheduledJobs.SingleAsync(x => x.Id == ids[2]).ConfigureAwait(true);
            Assert.Equal((true, 7, true), (untouched.AlertEnabled, untouched.AlertOverSeconds, untouched.AlertActive));
        }

        var disable = new BatchUpdateScheduledJobAlertRequest { Ids = [ids[0]], AlertEnabled = false };
        _ = await NewController(AllDataScope()).BatchUpdateAlertAsync(disable).ConfigureAwait(true);
        await using (var check = NewDb(AllDataScope())) {
            var off = await check.ScheduledJobs.SingleAsync(x => x.Id == ids[0]).ConfigureAwait(true);
            Assert.Equal((false, false), (off.AlertEnabled, off.AlertActive));
            var stillOn = await check.ScheduledJobs.SingleAsync(x => x.Id == ids[1]).ConfigureAwait(true);
            Assert.True(stillOn.AlertActive);
        }
    }

    /// <summary>
    ///     验证五类映射中关联对象已不存在的记录被删除，正常映射保留
    /// </summary>
    [Fact]
    public async Task Cleanup_DeletesOnlyOrphansAsync() {
        await SeedAsync().ConfigureAwait(true);

        var response = await NewController(AllDataScope()).CleanupOrphanMappingsAsync().ConfigureAwait(true);

        var data = ((OkObjectResult)response.Result!).Value as ApiResponse<CleanupOrphanMappingsResult>;
        Assert.Equal(new CleanupOrphanMappingsResult(2, 1, 2, 1, 1), data!.Data);
        Assert.Equal(7, data.Data!.Total);

        await using var db = NewDb(AllDataScope());
        Assert.Equal(1, await db.UserRoles.CountAsync().ConfigureAwait(true));
        Assert.Equal(1, await db.UserDepartments.CountAsync().ConfigureAwait(true));
        Assert.Equal(1, await db.RoleMenus.CountAsync().ConfigureAwait(true));
        Assert.Equal(1, await db.RoleMenuAuths.CountAsync().ConfigureAwait(true));
        Assert.Equal(1, await db.RoleApis.CountAsync().ConfigureAwait(true));
        var keptUserRole = await db.UserRoles.SingleAsync().ConfigureAwait(true);
        Assert.Equal((1L, 10L), (keptUserRole.UserId, keptUserRole.RoleId));
    }

    /// <summary>
    ///     验证没有孤儿映射时不删除任何数据，重复执行结果稳定
    /// </summary>
    [Fact]
    public async Task Cleanup_IsIdempotentAsync() {
        await SeedAsync().ConfigureAwait(true);
        _ = await NewController(AllDataScope()).CleanupOrphanMappingsAsync().ConfigureAwait(true);

        var response = await NewController(AllDataScope()).CleanupOrphanMappingsAsync().ConfigureAwait(true);

        var data = ((OkObjectResult)response.Result!).Value as ApiResponse<CleanupOrphanMappingsResult>;
        Assert.Equal(0, data!.Data!.Total);
    }

    /// <summary>
    ///     验证非全部数据权限不能执行清理
    /// </summary>
    [Fact]
    public async Task Cleanup_RequiresAllDataScopeAsync() {
        var scope = new DataScopeContext();
        scope.Initialize(10, false, true, new HashSet<long>(), 100);

        _ = await Assert.ThrowsAsync<DataAccessDeniedException>(() => NewController(scope).CleanupOrphanMappingsAsync()).ConfigureAwait(true);
    }

    /// <summary>
    ///     释放内存数据库连接
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
    }

    /// <summary>
    ///     验证系统种子创建了清理作业和所需占位符，作业默认未启用，重复执行不会重复创建也不会覆盖已填写的值
    /// </summary>
    [Fact]
    public async Task Seed_CreatesDisabledJobAndPlaceholdersIdempotentlyAsync() {
        await using (var db = NewDb(AllDataScope())) {
            await SeedScheduledJobAsync(db).ConfigureAwait(true);
        }

        await using (var db = NewDb(AllDataScope())) {
            var token = await db.DictionaryItems.SingleAsync(x => x.Label == "JobToken").ConfigureAwait(true);
            token.Value = "filled-by-admin";
            _ = await db.SaveChangesAsync().ConfigureAwait(true);
            await SeedScheduledJobAsync(db).ConfigureAwait(true);
        }

        await using var check = NewDb(AllDataScope());
        var job = await check.ScheduledJobs.SingleAsync(x => x.Name == "Cleanup orphan mappings").ConfigureAwait(true);
        Assert.False(job.IsEnabled);
        Assert.Equal("POST", job.RequestMethod);
        Assert.Equal("{{ApiBaseUrl}}/api/scheduled-job/orphan-mappings/cleanup", job.RequestUrl);
        Assert.Contains("{{JobToken}}", job.RequestHeadersJson, StringComparison.Ordinal);
        var labels = await check.DictionaryItems.Select(x => x.Label).OrderBy(x => x).ToListAsync().ConfigureAwait(true);
        Assert.Equal(["ApiBaseUrl", "JobToken"], labels);
        var kept = await check.DictionaryItems.SingleAsync(x => x.Label == "JobToken").ConfigureAwait(true);
        Assert.Equal("filled-by-admin", kept.Value);
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
    ///     通过反射调用系统种子里 internal 的计划作业种子方法，保持被测代码与生产一致
    /// </summary>
    /// <param name="db">数据库上下文</param>
    private static Task SeedScheduledJobAsync(AppDbContext db) {
        var seeder = typeof(DatabaseInitializer).Assembly.GetType("AiAdmin.Api.Data.SystemDataSeeder")!;
        var method = seeder.GetMethod("EnsureScheduledJobSeedAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task)method.Invoke(null, [db])!;
    }

    /// <summary>
    ///     创建计划作业控制器，缓存依赖在没有孤儿映射的场景下不会被调用
    /// </summary>
    /// <param name="scope">数据权限上下文</param>
    /// <returns>计划作业管理控制器</returns>
    private ScheduledJobsController NewController(DataScopeContext scope) {
        var cacheOptions = Options.Create(new MemoryDistributedCacheOptions());
        var apiCache = new ApiPermissionCache(new MemoryDistributedCache(cacheOptions), null!);
        return new ScheduledJobsController(NewDb(scope), null!, null!, new DataScopeCache(FakeRedis.Create()), apiCache);
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
    ///     写入正常映射和孤儿映射：关闭外键后直接插入，模拟历史遗留数据
    /// </summary>
    private async Task SeedAsync() {
        await using var db = NewDb(AllDataScope());
        _ = await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF").ConfigureAwait(true);
        _ = db.Users.Add(new User { Id = 1, UserName = "u1", PasswordHash = "x" });
        _ = db.Roles.Add(new Role { Id = 10, Code = "R_TEST", Name = "Test" });
        _ = db.Departments.Add(new Department { Id = 20, Code = "D1", Name = "D1" });
        _ = db.Menus.Add(
            new Menu
            {
                Id = 30
                , Name = "M1"
                , Path = "m1"
                , Component = "/m1"
                , MetaJson = "{}"
            }
        );
        _ = db.ApiEndpoints.Add(new ApiEndpoint { Id = 40, Method = "GET", Path = "/api/x" });
        _ = await db.SaveChangesAsync().ConfigureAwait(true);

        // 正常映射各一条；孤儿映射：用户角色 2 条（用户不存在、角色不存在）、用户部门 1 条、角色菜单 2 条、按钮权限 1 条、角色接口 1 条
        _ = await db
            .Database.ExecuteSqlRawAsync(
                """
                INSERT INTO sys_user_role (UserId, RoleId, CreatedAt) VALUES (1, 10, '2026-01-01'), (999, 10, '2026-01-01'), (1, 999, '2026-01-01');
                INSERT INTO sys_user_department (UserId, DepartmentId, CreatedAt) VALUES (1, 20, '2026-01-01'), (1, 999, '2026-01-01');
                INSERT INTO sys_role_menu (RoleId, MenuId, CreatedAt) VALUES (10, 30, '2026-01-01'), (10, 999, '2026-01-01'), (999, 30, '2026-01-01');
                INSERT INTO sys_role_menu_auth (RoleId, MenuId, AuthMark, CreatedAt) VALUES (10, 30, 'delete', '2026-01-01'), (10, 999, 'delete', '2026-01-01');
                INSERT INTO sys_role_api (RoleId, ApiEndpointId, CreatedAt) VALUES (10, 40, '2026-01-01'), (10, 999, '2026-01-01');
                """
            )
            .ConfigureAwait(true);
    }

    /// <summary>
    ///     最小化的假 Redis 数据库：异步方法返回已完成的任务
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
    ///     最小化的假 Redis 连接：只支持 GetDatabase，数据库代理对任意调用返回已完成的结果，供缓存失效调用使用
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