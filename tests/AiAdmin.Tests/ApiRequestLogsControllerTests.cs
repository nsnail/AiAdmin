using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证请求日志列表接口：按创建时间范围选择日分表、跨表合并排序分页、可读筛选翻译、字段还原以及范围和权限限制
/// </summary>
public sealed class ApiRequestLogsControllerTests : IAsyncLifetime, IDisposable
{
    private static readonly string[] _userListOriginals = ["User list"];
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ApiRequestLogTableService _tables;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApiRequestLogsControllerTests" /> class.
    ///     初始化主库、分表服务和测试数据：两个接口、两个用户、三天的日志
    /// </summary>
    public ApiRequestLogsControllerTests() {
        var connectionString = $"DataSource=file:api-log-list-{Guid.NewGuid():N}?mode=memory&cache=shared";
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
        _ = db.ApiEndpoints.Add(
            new ApiEndpoint
            {
                Name = "Role list", Method = "POST", Path = "/api/role/list", PathCrc32 = ApiEndpointKey.ComputePathCrc32("/api/role/list")
            }
        );
        _ = db.Users.Add(new User { Id = 10, UserName = "alice", PasswordHash = "x" });
        _ = db.SaveChanges();
    }

    /// <summary>
    ///     验证接口远程搜索下拉使用的筛选条件：路径或英文说明包含关键字，或英文说明属于中文关键字反查出的原文，任一命中即可，Or 嵌套能被接口列表正确处理
    /// </summary>
    [Fact]
    public async Task ApiEndpointList_SupportsRemoteSearchFilterAsync() {
        await using var db = NewDb(true);
        var controller = new ApiEndpointsController(db, null!, null!);
        var byPath = new DynamicFilter { Logic = "Or", Filters = [Condition("Path", "Contains", "role"), Condition("Name", "Contains", "role")] };
        var byOriginal = new DynamicFilter
        {
            Logic = "Or", Filters = [Condition("Path", "Contains", "用户"), Condition("Name", "Any", _userListOriginals)]
        };

        var rolePaths = await ListApisAsync(controller, byPath).ConfigureAwait(true);
        var originalPaths = await ListApisAsync(controller, byOriginal).ConfigureAwait(true);

        Assert.Equal(["/api/role/list"], rolePaths);
        Assert.Equal(["/api/user/list"], originalPaths);
    }

    /// <summary>
    ///     释放服务和数据库连接
    /// </summary>
    public void Dispose() {
        _tables.Dispose();
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     测试结束时无需额外异步清理，资源由 Dispose 释放
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task DisposeAsync() {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     验证筛选字段元数据：创建时间排第一，包含方法、接口、用户、IP、状态码
    /// </summary>
    [Fact]
    public void FilterFields_StartsWithCreatedAtAndDeclaresReadableFields() {
        var fields = Assert.IsType<OkObjectResult>(NewController(true).FilterFields().Result);
        var list = Assert.IsType<ApiResponse<IReadOnlyList<ListFilterFieldResult>>>(fields.Value, false).Data!;

        Assert.Equal(nameof(ApiRequestLog.CreatedAt), list[0].Field);
        Assert.Equal("select", list.Single(x => x.Field == nameof(ApiRequestLog.Method)).Control);
        Assert.Equal("user-select", list.Single(x => x.Field == nameof(ApiRequestLog.UserId)).Control);
        Assert.Contains(list, x => x.Field == nameof(ApiRequestLog.ClientIp));
        Assert.Contains(list, x => x.Field == nameof(ApiRequestLog.PathCrc32));
        Assert.Contains(list, x => x.Field == nameof(ApiRequestLog.StatusCode));
    }

    /// <summary>
    ///     复现：按一个今天没有日志的接口筛选时，列表为空；接口按钮组展示的是排除本字段筛选后的各接口数量，并不为空，
    ///     而同时受其他字段筛选约束的方法、状态码分组则与列表一致，都是 0
    /// </summary>
    [Fact]
    public async Task FilterGroups_ApiGroupIgnoresOwnSelectionWhileOtherGroupsFollowListAsync() {
        const int noLogsForThisApi = -281398365;
        var request = Request(Today(), Today().AddDays(1), extra: Condition("PathCrc32", "Equal", noLogsForThisApi));

        var list = await ListAsync(request).ConfigureAwait(true);
        var groups = await GroupsAsync(request).ConfigureAwait(true);

        Assert.Equal(0, list.Total);
        Assert.Equal(3, groups.Single(x => x.Field == nameof(ApiRequestLog.PathCrc32)).Options.Sum(x => x.Count));
        Assert.Equal(0, groups.Single(x => x.Field == nameof(ApiRequestLog.Method)).Options.Sum(x => x.Count));
        Assert.Equal(0, groups.Single(x => x.Field == nameof(ApiRequestLog.StatusCode)).Options.Sum(x => x.Count));
    }

    /// <summary>
    ///     验证某个分组字段自己的筛选条件不影响本字段的计数，其他字段的筛选会影响
    /// </summary>
    [Fact]
    public async Task FilterGroups_ExcludesOwnFieldFilterAsync() {
        var userListCrc = ApiEndpointKey.ComputePathCrc32("/api/user/list");

        var groups = await GroupsAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("PathCrc32", "Equal", userListCrc)))
            .ConfigureAwait(true);

        Assert.Equal(3, groups.Single(x => x.Field == nameof(ApiRequestLog.PathCrc32)).Options.Count);
        Assert.Equal(4, groups.Single(x => x.Field == nameof(ApiRequestLog.StatusCode)).Total);
    }

    /// <summary>
    ///     验证范围内没有日表时仍返回各分组字段，选项为空，不报错；没有全部数据权限不能查询
    /// </summary>
    [Fact]
    public async Task FilterGroups_HandlesEmptyRangeAndRequiresAllDataScopeAsync() {
        var groups = await GroupsAsync(Request(Today().AddDays(-30), Today().AddDays(-25))).ConfigureAwait(true);

        Assert.Equal(3, groups.Count);
        Assert.All(groups, x => Assert.DoesNotContain(x.Options, option => option.Count > 0));
        _ = await Assert
            .ThrowsAsync<DataAccessDeniedException>(() => NewController(false).FilterGroupsAsync(new ListFilterGroupRequest()))
            .ConfigureAwait(true);
    }

    /// <summary>
    ///     验证分组计数跨日表合并，与列表总数同口径：方法只有 POST，状态码 200 共 7 条
    /// </summary>
    [Fact]
    public async Task FilterGroups_MergesCountsAcrossDailyTablesAsync() {
        var groups = await GroupsAsync(Request(Today().AddDays(-2), Today().AddDays(1))).ConfigureAwait(true);

        var method = groups.Single(x => x.Field == nameof(ApiRequestLog.Method));
        Assert.Equal(7, method.Options.Single(x => Convert.ToInt32(x.Value, CultureInfo.InvariantCulture) == 2).Count);
        Assert.Equal(0, method.Options.Single(x => Convert.ToInt32(x.Value, CultureInfo.InvariantCulture) == 1).Count);
        var status = groups.Single(x => x.Field == nameof(ApiRequestLog.StatusCode));
        Assert.Equal(7, status.Options.Single(x => Convert.ToInt32(x.Value, CultureInfo.InvariantCulture) == 200).Count);
        Assert.Equal(7, status.Total);
    }

    /// <summary>
    ///     验证接口分组按数量倒序，并把 CRC32 还原为接口说明，接口表里没有的保留数值
    /// </summary>
    [Fact]
    public async Task FilterGroups_RestoresApiNameAndOrdersByCountAsync() {
        var groups = await GroupsAsync(Request(Today().AddDays(-2), Today().AddDays(1))).ConfigureAwait(true);

        var api = groups.Single(x => x.Field == nameof(ApiRequestLog.PathCrc32));
        Assert.Equal(
            ["User list", "Role list", ApiEndpointKey.ComputePathCrc32("/api/unknown").ToString(CultureInfo.InvariantCulture)]
            , api.Options.Select(x => x.Label)
        );
        Assert.Equal([4, 2, 1], api.Options.Select(x => x.Count));
    }

    /// <summary>
    ///     每个测试开始前异步写入三天的日志
    /// </summary>
    /// <returns>异步任务</returns>
    public async Task InitializeAsync() {
        _ = await SeedLogsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     验证展示列名可作为排序字段：用户列按用户主键排序，接口列按路径 CRC32 排序，而不是报字段不存在
    /// </summary>
    [Fact]
    public async Task List_AcceptsDisplayColumnNamesAsSortFieldsAsync() {
        var byUser = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), sortField: "userName", sortOrder: "asc")).ConfigureAwait(true);
        var byPath = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), sortField: "path", sortOrder: "desc")).ConfigureAwait(true);

        Assert.Equal(7, byUser.Total);
        Assert.Equal(
            byUser.Records.Select(x => x.UserId is null ? 0L : long.Parse(x.UserId, CultureInfo.InvariantCulture)).Order()
            , byUser.Records.Select(x => x.UserId is null ? 0L : long.Parse(x.UserId, CultureInfo.InvariantCulture))
        );
        Assert.Equal(byPath.Records.Select(x => x.PathCrc32).OrderDescending(), byPath.Records.Select(x => x.PathCrc32));
    }

    /// <summary>
    ///     验证未指定范围时只查今天
    /// </summary>
    [Fact]
    public async Task List_DefaultsToTodayAsync() {
        var page = await ListAsync(new DynamicQueryRequest()).ConfigureAwait(true);

        Assert.Equal(3, page.Total);
        Assert.All(page.Records, x => Assert.Equal(Today().Date, x.CreatedAt.UtcDateTime.Date));
    }

    /// <summary>
    ///     验证方法既可用数字也可用方法名筛选，右键筛选传的是列表里显示的方法名文本
    /// </summary>
    [Fact]
    public async Task List_FiltersByMethodNameOrNumberAsync() {
        var byName = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("Method", "Equal", "POST")))
            .ConfigureAwait(true);
        var byNumber = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("Method", "Equal", 2))).ConfigureAwait(true);
        var none = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("Method", "Equal", "GET"))).ConfigureAwait(true);

        Assert.Equal(7, byName.Total);
        Assert.Equal(7, byNumber.Total);
        Assert.Equal(0, none.Total);
    }

    /// <summary>
    ///     验证客户端 IP 以点分文本筛选并翻译为整数，结果中 IP 还原为文本
    /// </summary>
    [Fact]
    public async Task List_FiltersByReadableClientIpAsync() {
        var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("ClientIp", "Equal", "192.168.1.10")))
            .ConfigureAwait(true);

        Assert.NotEmpty(page.Records);
        Assert.All(page.Records, x => Assert.Equal("192.168.1.10", x.ClientIp));
        Assert.All(page.Records, x => Assert.Equal("10.0.0.5", x.ServerIp));
    }

    /// <summary>
    ///     验证范围内不存在的日表被忽略，不报错
    /// </summary>
    [Fact]
    public async Task List_IgnoresMissingDailyTablesAsync() {
        var page = await ListAsync(Request(Today().AddDays(-30), Today().AddDays(-25))).ConfigureAwait(true);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Records);
    }

    /// <summary>
    ///     验证日期范围跨多天时合并多张日表，默认按创建时间倒序，主键作为次级排序
    /// </summary>
    [Fact]
    public async Task List_MergesDailyTablesInDescendingOrderAsync() {
        var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1))).ConfigureAwait(true);

        Assert.Equal(7, page.Total);
        Assert.Equal(
            page.Records.OrderByDescending(x => x.CreatedAt).ThenBy(x => long.Parse(x.Id, CultureInfo.InvariantCulture)).Select(x => x.Id)
            , page.Records.Select(x => x.Id)
        );
        Assert.Equal(Today().Date, page.Records[0].CreatedAt.UtcDateTime.Date);
        Assert.Equal(Today().AddDays(-2).Date, page.Records[^1].CreatedAt.UtcDateTime.Date);
    }

    /// <summary>
    ///     验证跨表分页：翻页不重复不遗漏，总数正确
    /// </summary>
    [Fact]
    public async Task List_PagesAcrossTablesStablyAsync() {
        var all = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), size: 100)).ConfigureAwait(true);
        var collected = new List<string>();
        for (var current = 1; current <= 4; current++) {
            var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), current, 2)).ConfigureAwait(true);
            collected.AddRange(page.Records.Select(x => x.Id));
            Assert.Equal(7, page.Total);
        }

        Assert.Equal(all.Records.Select(x => x.Id), collected);
        Assert.Equal(7, collected.Distinct().Count());
    }

    /// <summary>
    ///     验证范围超过 7 天、起止颠倒或 IP 非法时返回明确的筛选错误，而不是静默忽略
    /// </summary>
    [Fact]
    public async Task List_RejectsInvalidInputAsync() {
        _ = await Assert
            .ThrowsAsync<DynamicFilterValidationException>(() => ListAsync(Request(Today().AddDays(-10), Today().AddDays(1))))
            .ConfigureAwait(true);
        _ = await Assert
            .ThrowsAsync<DynamicFilterValidationException>(() => ListAsync(Request(Today().AddDays(1), Today().AddDays(-3))))
            .ConfigureAwait(true);
        _ = await Assert
            .ThrowsAsync<DynamicFilterValidationException>(() =>
                ListAsync(Request(Today().AddDays(-1), Today().AddDays(1), extra: Condition("ClientIp", "Equal", "not-an-ip")))
            )
            .ConfigureAwait(true);
        _ = await Assert
            .ThrowsAsync<DynamicFilterValidationException>(() =>
                ListAsync(Request(Today().AddDays(-1), Today().AddDays(1), extra: Condition("Missing", "Equal", 1)))
            )
            .ConfigureAwait(true);
        _ = await Assert
            .ThrowsAsync<DynamicFilterValidationException>(() =>
                ListAsync(Request(Today().AddDays(-1), Today().AddDays(1), sortField: "Missing", sortOrder: "asc"))
            )
            .ConfigureAwait(true);
    }

    /// <summary>
    ///     验证没有全部数据权限的用户不能查询请求日志
    /// </summary>
    [Fact]
    public async Task List_RequiresAllDataScopeAsync() {
        var controller = NewController(false);

        _ = await Assert.ThrowsAsync<DataAccessDeniedException>(() => controller.ListAsync(new DynamicQueryRequest())).ConfigureAwait(true);
    }

    /// <summary>
    ///     验证按接口筛选，结果还原接口名称和路径，按用户筛选还原用户名
    /// </summary>
    [Fact]
    public async Task List_RestoresApiAndUserAsync() {
        var crc = ApiEndpointKey.ComputePathCrc32("/api/user/list");

        var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("PathCrc32", "Equal", crc)))
            .ConfigureAwait(true);

        Assert.NotEmpty(page.Records);
        Assert.All(page.Records, x => Assert.Equal("/api/user/list", x.Path));
        Assert.All(page.Records, x => Assert.Equal("User list", x.ApiName));
        Assert.All(page.Records, x => Assert.Equal("POST", x.Method));
        Assert.Contains(page.Records, x => x.UserName == "alice");
    }

    /// <summary>
    ///     验证无法还原的接口返回空路径，不影响列表
    /// </summary>
    [Fact]
    public async Task List_ReturnsNullPathForUnknownEndpointAsync() {
        var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), extra: Condition("PathCrc32", "Equal", 12345)))
            .ConfigureAwait(true);

        Assert.Empty(page.Records);
        var all = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1))).ConfigureAwait(true);
        Assert.Contains(all.Records, x => x.Path is null && x.ApiName is null);
    }

    /// <summary>
    ///     验证按耗时升序排序，跨表合并后整体有序
    /// </summary>
    [Fact]
    public async Task List_SortsByFieldAcrossTablesAsync() {
        var page = await ListAsync(Request(Today().AddDays(-2), Today().AddDays(1), sortField: "ElapsedMilliseconds", sortOrder: "asc"))
            .ConfigureAwait(true);

        Assert.Equal(page.Records.Select(x => x.ElapsedMilliseconds).Order(), page.Records.Select(x => x.ElapsedMilliseconds));
    }

    /// <summary>
    ///     验证 ES 未启用时返回空结果而不是报错，没有全部数据权限时拒绝，非法链路编号被拒绝
    /// </summary>
    [Fact]
    public async Task TraceLogs_HandlesDisabledEsAndRejectsInvalidInputAsync() {
        var disabled = await NewController(true, null, false).TraceLogsAsync(new ApiRequestLogTraceRequest { TraceId = "1" }).ConfigureAwait(true);
        var page = Assert.IsType<ApiResponse<PagedResponse<SystemLogItem>>>(Assert.IsType<OkObjectResult>(disabled.Result).Value, false).Data!;
        Assert.Empty(page.Records);
        Assert.Equal(0, page.Total);

        _ = await Assert
            .ThrowsAsync<DataAccessDeniedException>(() => NewController(false).TraceLogsAsync(new ApiRequestLogTraceRequest { TraceId = "1" }))
            .ConfigureAwait(true);
        var invalid = new ValidationContext(new ApiRequestLogTraceRequest { TraceId = "abc" });
        Assert.False(await Validator.TryValidateObjectAsync(invalid.ObjectInstance, invalid, [], true).ConfigureAwait(true));
    }

    /// <summary>
    ///     验证按链路编号查询 ES：请求体按 TraceId 精确过滤、按时间升序，返回的文档能反序列化为日志项
    /// </summary>
    [Fact]
    public async Task TraceLogs_QueriesElasticsearchByTraceIdAsync() {
        const string hits = "{\"hits\":{\"total\":{\"value\":2},\"hits\":["
                            + "{\"_source\":{\"timestamp\":\"2026-10-27T01:00:00+00:00\",\"level\":\"Information\",\"logType\":\"Api\",\"message\":\"API request completed\",\"traceId\":123456789012345678,\"statusCode\":200}},"
                            + "{\"_source\":{\"timestamp\":\"2026-10-27T01:00:01+00:00\",\"level\":\"Information\",\"logType\":\"Sql\",\"message\":\"SQL\",\"traceId\":123456789012345678,\"sql\":\"SELECT 1\"}}]}}";
        var es = new StubHttpMessageHandler(HttpStatusCode.OK, hits);

        var result = await NewController(true, es)
            .TraceLogsAsync(new ApiRequestLogTraceRequest { TraceId = "123456789012345678" })
            .ConfigureAwait(true);

        var page = Assert.IsType<ApiResponse<PagedResponse<SystemLogItem>>>(Assert.IsType<OkObjectResult>(result.Result).Value, false).Data!;
        Assert.Equal(2, page.Total);
        Assert.Equal(["Api", "Sql"], page.Records.Select(x => x.LogType));
        Assert.Equal("SELECT 1", page.Records[1].Sql);
        Assert.Equal(123456789012345678L, page.Records[0].TraceId);
        Assert.Contains("telehub-logs-*/_search", es.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("123456789012345678", es.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"term\":{\"traceId\":123456789012345678}", es.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"asc\"", es.RequestBody, StringComparison.Ordinal);
    }

    /// <summary>
    ///     创建单个筛选条件
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="operation">操作符</param>
    /// <param name="value">筛选值</param>
    /// <returns>动态筛选条件</returns>
    private static DynamicFilter Condition(
        string field
        , string operation
        , object value
    ) {
        return new DynamicFilter { Field = field, Operator = operation, Value = JsonSerializer.SerializeToElement(value) };
    }

    /// <summary>
    ///     调用接口列表并取出路径
    /// </summary>
    /// <param name="controller">接口管理控制器</param>
    /// <param name="filter">动态筛选条件</param>
    /// <returns>匹配的接口路径</returns>
    private static async Task<List<string>> ListApisAsync(
        ApiEndpointsController controller
        , DynamicFilter filter
    ) {
        var result = await controller
            .ListAsync(new DynamicQueryRequest { DynamicFilter = filter, SortField = "Path", SortOrder = "asc" })
            .ConfigureAwait(false);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return [.. Assert.IsType<ApiResponse<IReadOnlyList<ApiEndpointResult>>>(ok.Value, false).Data!.Select(x => x.Path)];
    }

    /// <summary>
    ///     创建指向桩处理器的 Elasticsearch 日志查询服务
    /// </summary>
    /// <param name="handler">桩 HTTP 消息处理器</param>
    /// <param name="enabled">是否启用 Elasticsearch 日志</param>
    /// <returns>Elasticsearch 日志查询服务</returns>
    private static ElasticsearchLogQueryService NewElasticsearch(
        StubHttpMessageHandler handler
        , bool enabled
    ) {
        var services = new ServiceCollection();
        _ = services.AddHttpClient(ExternalHttpRequestService.CLIENT_NAME).ConfigurePrimaryHttpMessageHandler(() => handler);
        var factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var http = new ExternalHttpRequestService(factory, new HttpContextAccessor(), NullLogger<ExternalHttpRequestService>.Instance);
        return new ElasticsearchLogQueryService(
            http, Options.Create(new ElasticsearchLogOptions { Enabled = enabled, Uri = "http://es.test:9200", Index = "telehub-logs" })
        );
    }

    /// <summary>
    ///     创建测试用日志
    /// </summary>
    /// <param name="createdAt">创建时间</param>
    /// <param name="path">请求路径，转换为路径 CRC32 保存</param>
    /// <param name="elapsed">耗时毫秒数</param>
    /// <param name="userId">用户主键</param>
    /// <returns>请求日志</returns>
    private static ApiRequestLog NewLog(
        DateTime createdAt
        , string path
        , int elapsed
        , long? userId
    ) {
        return new ApiRequestLog
        {
            CreatedAt = createdAt
            , Method = ApiRequestMethod.Post
            , PathCrc32 = ApiEndpointKey.ComputePathCrc32(path)
            , StatusCode = 200
            , ElapsedMilliseconds = elapsed
            , UserId = userId
            , ClientIp = unchecked((int)0xC0A8010A)
            , ServerIp = 0x0A000005
            , TraceId = 1
            , WorkerId = 1
        };
    }

    /// <summary>
    ///     创建带创建时间范围的列表请求
    /// </summary>
    /// <param name="from">起始时间，含</param>
    /// <param name="to">结束时间，不含</param>
    /// <param name="current">页码</param>
    /// <param name="size">每页记录数</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <param name="extra">额外的筛选条件</param>
    /// <returns>列表请求</returns>
    private static DynamicQueryRequest Request(
        DateTime from
        , DateTime to
        , int current = 1
        , int size = 100
        , string? sortField = null
        , string? sortOrder = null
        , DynamicFilter? extra = null
    ) {
        var range = Condition("CreatedAt", "DateRange", new[] { from.ToString("O"), to.ToString("O") });
        return new DynamicQueryRequest
        {
            Current = current
            , Size = size
            , SortField = sortField
            , SortOrder = sortOrder
            , DynamicFilter = new DynamicFilter { Logic = "And", Filters = extra is null ? [range] : [range, extra] }
        };
    }

    /// <summary>
    ///     今天零点的 UTC 日期
    /// </summary>
    /// <returns>今天零点的 UTC 时间</returns>
    private static DateTime Today() {
        return DateTime.UtcNow.Date;
    }

    /// <summary>
    ///     调用分组统计接口并取出分组结果
    /// </summary>
    /// <param name="request">列表请求，取其中的动态筛选条件</param>
    /// <returns>分组统计</returns>
    private async Task<IReadOnlyList<ListFilterGroupResult>> GroupsAsync(DynamicQueryRequest request) {
        var result = await NewController(true)
            .FilterGroupsAsync(new ListFilterGroupRequest { DynamicFilter = request.DynamicFilter })
            .ConfigureAwait(false);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>(ok.Value, false).Data!;
    }

    /// <summary>
    ///     调用列表接口并取出分页数据
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <returns>分页结果</returns>
    private async Task<PagedResponse<ApiRequestLogResult>> ListAsync(DynamicQueryRequest request) {
        var result = await NewController(true).ListAsync(request).ConfigureAwait(false);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<PagedResponse<ApiRequestLogResult>>>(ok.Value, false).Data!;
    }

    /// <summary>
    ///     创建请求日志控制器
    /// </summary>
    /// <param name="allData">是否拥有全部数据权限</param>
    /// <param name="elasticsearch">Elasticsearch 的桩 HTTP 处理器，未指定时返回空响应</param>
    /// <param name="elasticsearchEnabled">是否启用 Elasticsearch 日志</param>
    /// <returns>请求日志控制器</returns>
    private ApiRequestLogsController NewController(
        bool allData
        , StubHttpMessageHandler? elasticsearch = null
        , bool elasticsearchEnabled = true
    ) {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test"));
        return new ApiRequestLogsController(
            NewDb(allData), _tables, NewElasticsearch(elasticsearch ?? new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), elasticsearchEnabled)
        ) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
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
    ///     写入三天的日志：今天 3 条、昨天 2 条、前天 2 条，其中一条使用接口表里没有的路径
    /// </summary>
    /// <returns>异步任务</returns>
    private Task<int> SeedLogsAsync() {
        var today = Today();
        return _tables.InsertAsync(
            [
                NewLog(today.AddHours(9), "/api/user/list", 30, 10), NewLog(today.AddHours(10), "/api/role/list", 10, null)
                , NewLog(today.AddHours(11), "/api/user/list", 20, 10), NewLog(today.AddDays(-1).AddHours(9), "/api/user/list", 50, 10)
                , NewLog(today.AddDays(-1).AddHours(10), "/api/unknown", 5, null), NewLog(today.AddDays(-2).AddHours(9), "/api/role/list", 40, null)
                , NewLog(today.AddDays(-2).AddHours(10), "/api/user/list", 15, 10)
            ], CancellationToken.None
        );
    }
}