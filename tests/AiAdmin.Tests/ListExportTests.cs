using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;
using Role = AiAdmin.Api.Models.Role;

namespace AiAdmin.Tests;

/// <summary>
///     验证列表导出公共流程：导出顺序与列表一致、总数和上限正确，以及带列表的菜单种子都声明了导出按钮权限
/// </summary>
public sealed class ListExportTests : IDisposable
{
    private const int _ROW_COUNT = 6;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ListExportTests" /> class.
    ///     初始化内存数据库并写入创建时间依次递增的角色记录
    /// </summary>
    public ListExportTests() {
        var connectionString = $"DataSource=file:list-export-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using var db = NewDb();
        _ = db.Database.EnsureCreated();
        var baseTime = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= _ROW_COUNT; i++) {
            _ = db.Roles.Add(NewRole(i));
        }

        _ = db.SaveChanges();
        SetCreatedTimes(db, 1, _ROW_COUNT, baseTime);
    }

    /// <summary>
    ///     释放内存数据库连接
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
    }

    /// <summary>
    ///     验证导出沿用动态筛选，总数只统计匹配的记录
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Export_AppliesDynamicFilterAsync() {
        var filter = new DynamicFilter { Field = nameof(Role.Id), Operator = "GreaterThan", Value = JsonSerializer.SerializeToElement(4L) };
        var export = await ExportAsync(new DynamicExportRequest { DynamicFilter = filter }).ConfigureAwait(true);
        Assert.Equal([6L, 5L], export.Records.Select(x => x.RoleId));
        Assert.Equal(2, export.Total);
    }

    /// <summary>
    ///     验证显式正序排序与列表一致
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Export_AscendingOrderMatchesListAsync() {
        var export = await ExportAsync(new DynamicExportRequest { SortField = "CreatedAt", SortOrder = "asc" }).ConfigureAwait(true);
        Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L], export.Records.Select(x => x.RoleId));
    }

    /// <summary>
    ///     验证默认排序下导出的顺序与列表一致，按创建时间倒序，总数等于匹配记录数
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Export_DefaultOrderMatchesListAsync() {
        var export = await ExportAsync(new DynamicExportRequest()).ConfigureAwait(true);
        Assert.Equal([6L, 5L, 4L, 3L, 2L, 1L], export.Records.Select(x => x.RoleId));
        Assert.Equal(_ROW_COUNT, export.Total);
        Assert.True(export.Limit >= _ROW_COUNT);
        var list = await ListIdsAsync(1, _ROW_COUNT, null, null).ConfigureAwait(true);
        Assert.Equal(list, export.Records.Select(x => x.RoleId));
    }

    /// <summary>
    ///     验证导出不受列表分页页大小限制：记录数超过列表单页上限时仍完整导出
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Export_IsNotLimitedByListPageSizeAsync() {
        await using (var db = NewDb()) {
            for (var i = _ROW_COUNT + 1; i <= 130; i++) {
                _ = db.Roles.Add(NewRole(i));
            }

            _ = await db.SaveChangesAsync().ConfigureAwait(true);
            SetCreatedTimes(db, _ROW_COUNT + 1, 130, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc));
        }

        var export = await ExportAsync(new DynamicExportRequest()).ConfigureAwait(true);
        Assert.Equal(130, export.Records.Count);
        Assert.Equal(130, export.Total);
    }

    /// <summary>
    ///     验证无效排序字段返回明确错误，不会忽略排序后返回数据
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Export_RejectsInvalidSortFieldAsync() {
        _ = await Assert
            .ThrowsAnyAsync<Exception>(() => ExportAsync(new DynamicExportRequest { SortField = "NoSuchField", SortOrder = "asc" }))
            .ConfigureAwait(true);
    }

    /// <summary>
    ///     验证带列表的菜单种子都声明了导出按钮权限，页面的导出按钮才会对被授权的角色显示
    /// </summary>
    [Fact]
    public void MenuSeed_DeclaresExportButtonForListMenus() {
        string[] listComponents =
        [
            "/system/user", "/system/role", "/system/department", "/system/menu", "/system/dictionary", "/system/message"
            , "/system/circuit-breaker", "/system/rate-limit", "/system/alert-monitor", "/system/system-log", "/system/login-log"
            , "/system/api-request-log", "/finance/wallet"
        ];
        using var document = JsonDocument.Parse(File.ReadAllText(FindSeedPath()));
        var exportable = new HashSet<string>(StringComparer.Ordinal);
        CollectExportMenus(document.RootElement, exportable);
        foreach (var component in listComponents) {
            Assert.Contains(component, exportable);
        }
    }

    /// <summary>
    ///     递归收集声明了 export 按钮权限的菜单组件路径
    /// </summary>
    /// <param name="nodes">菜单节点数组</param>
    /// <param name="result">收集结果</param>
    private static void CollectExportMenus(
        JsonElement nodes
        , HashSet<string> result
    ) {
        foreach (var node in nodes.EnumerateArray()) {
            var component = node.GetProperty("component").GetString() ?? string.Empty;
            if (node.GetProperty("meta").TryGetProperty("authList", out var auths)
                && auths.EnumerateArray().Any(x => x.GetProperty("authMark").GetString() == "export")) {
                _ = result.Add(component);
            }

            if (node.TryGetProperty("children", out var children)) {
                CollectExportMenus(children, result);
            }
        }
    }

    /// <summary>
    ///     从测试输出目录向上查找仓库中的菜单种子文件
    /// </summary>
    /// <returns>菜单种子文件路径</returns>
    /// <exception cref="FileNotFoundException">向上查找不到菜单种子文件时引发</exception>
    private static string FindSeedPath() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            var candidate = Path.Combine(directory.FullName, "src", "AiAdmin.Infrastructure", "Data", "menu-seed.json");
            if (File.Exists(candidate)) {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("menu-seed.json was not found");
    }

    /// <summary>
    ///     创建测试用角色，编码和名称带主键以保证唯一
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <returns>角色实体</returns>
    private static Role NewRole(long id) {
        return new Role { Id = id, Code = $"R_EXPORT_{id}", Name = $"Export {id}" };
    }

    /// <summary>
    ///     保存会把创建时间统一改为当前时间，所以保存后按主键顺序把创建时间改写为依次递增，使排序结果可预期
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <param name="firstId">起始主键</param>
    /// <param name="lastId">结束主键</param>
    /// <param name="baseTime">起始创建时间，主键每加一创建时间增加一分钟</param>
    private static void SetCreatedTimes(
        AppDbContext db
        , int firstId
        , int lastId
        , DateTime baseTime
    ) {
        for (var id = firstId; id <= lastId; id++) {
            var time = baseTime.AddMinutes(id);
            var key = (long)id;
            _ = db.Roles.Where(x => x.Id == key).ExecuteUpdate(x => x.SetProperty(r => r.CreatedAt, time));
        }
    }

    /// <summary>
    ///     调用角色导出接口并返回导出结果
    /// </summary>
    /// <param name="request">导出请求</param>
    /// <returns>导出结果</returns>
    private async Task<DynamicExportResult<RoleListItem>> ExportAsync(DynamicExportRequest request) {
        await using var db = NewDb();
        var redis = FakeRedis.Create();
        var limits = new ExportLimitService(db, new DictionarySnapshotService(db, redis, NullLogger<DictionarySnapshotService>.Instance));
        var controller = new RolesController(db, null!, null!);
        var result = Assert.IsType<OkObjectResult>((await controller.ExportAsync(request, limits).ConfigureAwait(true)).Result);
        return Assert.IsType<ApiResponse<DynamicExportResult<RoleListItem>>>(result.Value).Data!;
    }

    /// <summary>
    ///     查询一页角色记录并返回主键顺序，用于和导出结果对照
    /// </summary>
    /// <param name="current">页码</param>
    /// <param name="size">每页条数</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <returns>当前页的主键顺序</returns>
    private async Task<List<long>> ListIdsAsync(
        int current
        , int size
        , string? sortField
        , string? sortOrder
    ) {
        await using var db = NewDb();
        var controller = new RolesController(db, null!, null!);
        var request = new DynamicQueryRequest { Current = current, Size = size, SortField = sortField, SortOrder = sortOrder };
        var result = Assert.IsType<OkObjectResult>((await controller.ListAsync(request).ConfigureAwait(true)).Result);
        return [.. Assert.IsType<ApiResponse<PagedResponse<RoleListItem>>>(result.Value).Data!.Records.Select(x => x.RoleId)];
    }

    /// <summary>
    ///     创建拥有全部数据权限的数据库上下文
    /// </summary>
    /// <returns>应用数据库上下文</returns>
    private AppDbContext NewDb() {
        var scope = new DataScopeContext();
        scope.Initialize(1, true, false, new HashSet<long>(), 100);
        return new AppDbContext(_options, scope, new DataScopeCache(FakeRedis.Create()));
    }

    /// <summary>
    ///     假的 Redis 数据库，异步方法返回已完成的默认结果
    /// </summary>
    [SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy subclasses cannot be sealed.")]
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
    ///     假的 Redis 连接复用器，读取总是未命中，写入被忽略，使导出上限回退到数据库和默认值
    /// </summary>
    [SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy subclasses cannot be sealed.")]
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