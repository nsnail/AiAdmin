using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     API 请求日志管理控制器，日志按天分表保存，查询按创建时间范围合并多张日表
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="tables">请求日志分表服务</param>
/// <param name="elasticsearch">Elasticsearch 日志查询服务</param>
[ApiController]
[Route("api/api-request-log")]
[ApiDescription("API request log management")]
public sealed class ApiRequestLogsController(AppDbContext db, ApiRequestLogTableService tables, ElasticsearchLogQueryService elasticsearch)
    : ControllerBase
{
    /// <summary>
    ///     列表展示列到实体字段的排序别名：用户列展示用户名，实际按用户主键排序；接口列展示路径，实际按路径 CRC32 排序
    /// </summary>
    private static readonly Dictionary<string, string> _sortAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["userName"] = nameof(ApiRequestLog.UserId), ["path"] = nameof(ApiRequestLog.PathCrc32), ["apiName"] = nameof(ApiRequestLog.PathCrc32)
    };

    /// <summary>
    ///     查询请求日志列表筛选字段元数据
    /// </summary>
    /// <returns>请求日志筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query API request log filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<ApiRequestLog>()));
    }

    /// <summary>
    ///     查询当前筛选条件下的字段分组计数，范围内每张日表各统计一次，再按分组值合并计数，计数口径与列表总数一致
    /// </summary>
    /// <param name="request">当前动态筛选条件和分组选项上限</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpPost("filter-groups")]
    [ApiDescription("Query API request log filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        var (from, to) = ApiRequestLogQueryRange.Resolve(request.DynamicFilter, DateTime.UtcNow);
        var filter = ApiRequestLogFilterTranslator.Translate(request.DynamicFilter);
        var names = await tables.ListTablesInRangeAsync(from, to, HttpContext.RequestAborted).ConfigureAwait(false);
        var perTable = new List<IReadOnlyList<ListFilterGroupResult>>();
        foreach (var name in names) {
            await using var context = tables.OpenQueryContext(name);

            // 每张表取全量分组值，合并后再截断，否则各表各取前几项会使合并后的计数不准
            perTable.Add(
                await ListFilterGroupingService.GetGroupsAsync(context.Logs.AsNoTracking(), filter, null, int.MaxValue).ConfigureAwait(false)
            );
        }

        var maxOptions = request.MaxOptions is > 0 ? request.MaxOptions.Value : 10;
        var groups = MergeGroups(perTable, [.. ListFilterMetadataService.GetFields<ApiRequestLog>().Where(x => x.GroupCount)], maxOptions);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(await RestoreGroupLabelsAsync(groups).ConfigureAwait(false)));
    }

    /// <summary>
    ///     分页查询请求日志，创建时间范围决定需要查询的日分表，范围未指定时只查今天，最多跨 7 天
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>请求日志分页结果</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpPost("list")]
    [ApiDescription("Query API request log list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ApiRequestLogResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        // 请求日志包含所有用户的访问记录，不属于任何数据范围，只有全部数据权限才能查看
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        var (from, to) = ApiRequestLogQueryRange.Resolve(request.DynamicFilter, DateTime.UtcNow);
        var filter = ApiRequestLogFilterTranslator.Translate(request.DynamicFilter);
        var names = await tables.ListTablesInRangeAsync(from, to, HttpContext.RequestAborted).ConfigureAwait(false);
        var take = request.Current * request.Size;
        var total = 0;
        var candidates = new List<ApiRequestLog>();
        foreach (var name in names) {
            await using var context = tables.OpenQueryContext(name);
            var query = context.Logs.AsNoTracking().ApplyDynamicFilter(filter);
            total += await query.CountAsync(HttpContext.RequestAborted).ConfigureAwait(false);

            // 每张日表只取排在前 current*size 行，合并后的前 current*size 行一定包含在这些行中
            var sorted = (IOrderedQueryable<ApiRequestLog>)query.ApplyDynamicSort(
                request.SortField, request.SortOrder, nameof(ApiRequestLog.CreatedAt), true, _sortAliases
            );
            candidates.AddRange(await sorted.ThenBy(x => x.Id).Take(take).ToListAsync(HttpContext.RequestAborted).ConfigureAwait(false));
        }

        var page = SortAndPage(candidates, ResolveSortField(request.SortField), request.SortOrder, request.Current, request.Size);
        var results = await ToResultsAsync(page).ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<ApiRequestLogResult>>.Ok(new PagedResponse<ApiRequestLogResult>(results, request.Current, request.Size, total))
        );
    }

    /// <summary>
    ///     按链路编号查询 Elasticsearch 中同一次请求产生的全部日志（HTTP 快照、SQL、外部请求、应用日志），按时间升序返回，最多 100 条
    /// </summary>
    /// <param name="request">包含链路编号的请求体</param>
    /// <returns>该链路的日志，Elasticsearch 未启用或没有记录时返回空集合</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpPost("trace-logs")]
    [ApiDescription("Query Elasticsearch logs by trace id")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SystemLogItem>>>> TraceLogsAsync([FromBody] ApiRequestLogTraceRequest request) {
        // Elasticsearch 日志含请求体、请求头和 SQL，与系统日志页一致，只允许全部数据权限查看
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        var query = new DynamicQueryRequest
        {
            Current = 1
            , Size = 100
            , SortField = nameof(SystemLogItem.Timestamp)
            , SortOrder = "asc"
            , DynamicFilter = new DynamicFilter
            {
                Field = nameof(SystemLogItem.TraceId), Operator = "Equal", Value = JsonSerializer.SerializeToElement(request.TraceId)
            }
        };
        var (records, total) = await elasticsearch.SearchAsync(query, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<PagedResponse<SystemLogItem>>.Ok(new PagedResponse<SystemLogItem>(records, 1, 100, total)));
    }

    /// <summary>
    ///     比较两个排序键，空值排在最小，其余按可比较类型比较
    /// </summary>
    /// <param name="left">左值</param>
    /// <param name="right">右值</param>
    /// <returns>比较结果</returns>
    private static int CompareValues(
        object? left
        , object? right
    ) {
        return left is null
            ? right is null ? 0 : -1
            : right is null
                ? 1
                : ((IComparable)left).CompareTo(right);
    }

    /// <summary>
    ///     把各日表的分组结果按字段和分组值合并，计数相加，再按数量倒序、名称升序截断
    /// </summary>
    /// <param name="perTable">每张日表的分组结果</param>
    /// <param name="fields">需要分组的字段元数据，决定输出顺序，即使没有日表也输出空分组</param>
    /// <param name="maxOptions">每个分组最多保留的选项数量</param>
    /// <returns>合并后的分组结果</returns>
    private static List<ListFilterGroupResult> MergeGroups(
        List<IReadOnlyList<ListFilterGroupResult>> perTable
        , ListFilterFieldResult[] fields
        , int maxOptions
    ) {
        return
        [
            .. fields.Select(field =>
                {
                    var groups = perTable.Select(tableGroups => tableGroups.Single(x => x.Field == field.Field)).ToArray();
                    var merged = groups
                        .SelectMany(x => x.Options)
                        .GroupBy(x => Convert.ToString(x.Value, CultureInfo.InvariantCulture) ?? string.Empty)
                        .Select(g => new ListFilterGroupOptionResult(g.First().Value, g.First().Label, g.Sum(x => x.Count)))
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                        .Take(maxOptions)
                        .ToArray();
                    return new ListFilterGroupResult(field.Field, field.Label, field.ValueType, groups.Sum(x => x.Total), merged);
                }
            )
        ];
    }

    /// <summary>
    ///     把展示列名映射为实体字段名，与数据库内排序使用同一份别名，保证各日表内排序与合并后排序口径一致
    /// </summary>
    /// <param name="sortField">客户端传入的排序字段</param>
    /// <returns>实体字段名，未传时返回原值</returns>
    private static string? ResolveSortField(string? sortField) {
        var trimmed = sortField?.Trim();
        return trimmed is not null && _sortAliases.TryGetValue(trimmed, out var mapped) ? mapped : trimmed;
    }

    /// <summary>
    ///     按排序字段对合并后的候选日志排序并取出当前页，排序规则与数据库内排序一致，主键作为次级排序保证分页稳定
    /// </summary>
    /// <param name="candidates">各日表取出的候选日志</param>
    /// <param name="sortField">排序字段名称</param>
    /// <param name="sortOrder">排序方向</param>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <returns>当前页日志</returns>
    /// <exception cref="DynamicFilterValidationException">排序字段不存在时引发</exception>
    private static List<ApiRequestLog> SortAndPage(
        List<ApiRequestLog> candidates
        , string? sortField
        , string? sortOrder
        , int current
        , int size
    ) {
        var descending = string.IsNullOrWhiteSpace(sortField) || string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var property = typeof(ApiRequestLog).GetProperty(
                           string.IsNullOrWhiteSpace(sortField) ? nameof(ApiRequestLog.CreatedAt) : sortField.Trim()
                           , BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
                       )
                       ?? throw new DynamicFilterValidationException($"Dynamic sort field '{sortField}' is not available.");
        var comparer = Comparer<object?>.Create(CompareValues);
        var ordered = descending ? candidates.OrderByDescending(property.GetValue, comparer) : candidates.OrderBy(property.GetValue, comparer);
        return [.. ordered.ThenBy(x => x.Id).Skip((current - 1) * size).Take(size)];
    }

    /// <summary>
    ///     把 IPv4 的 32 位整数还原为点分十进制文本
    /// </summary>
    /// <param name="value">32 位整数</param>
    /// <returns>点分十进制文本，为空时返回 null</returns>
    private static string? ToIpText(int? value) {
        return value is { } ip ? new IPAddress([(byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip]).ToString() : null;
    }

    /// <summary>
    ///     把方法编号还原为方法名称
    /// </summary>
    /// <param name="method">方法编号</param>
    /// <returns>方法名称，未知方法返回 Other</returns>
    private static string ToMethodName(ApiRequestMethod method) {
        return method == ApiRequestMethod.Other ? "Other" : method.ToString().ToUpperInvariant();
    }

    /// <summary>
    ///     把接口分组里的 CRC32 还原为接口说明和路径，接口表里找不到的保留数值
    /// </summary>
    /// <param name="groups">合并后的分组结果</param>
    /// <returns>还原显示名称后的分组结果</returns>
    private async Task<IReadOnlyList<ListFilterGroupResult>> RestoreGroupLabelsAsync(List<ListFilterGroupResult> groups) {
        var apiGroup = groups.Find(x => x.Field == nameof(ApiRequestLog.PathCrc32));
        if (apiGroup is null || apiGroup.Options.Count == 0) {
            return groups;
        }

        var crcs = apiGroup.Options.Select(x => Convert.ToInt32(x.Value, CultureInfo.InvariantCulture)).ToList();
        var apis = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => crcs.Contains(x.PathCrc32))
            .Select(x => new { x.PathCrc32, x.Path })
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        var pathByCrc = apis.GroupBy(x => x.PathCrc32).ToDictionary(x => x.Key, x => x.First().Path);
        var restored = apiGroup
            .Options.Select(option =>
                {
                    var crc = Convert.ToInt32(option.Value, CultureInfo.InvariantCulture);
                    return option with { Label = pathByCrc.TryGetValue(crc, out var path) ? path : option.Label };
                }
            )
            .ToArray();
        return [.. groups.Select(x => x.Field == nameof(ApiRequestLog.PathCrc32) ? x with { Options = restored } : x)];
    }

    /// <summary>
    ///     把日志转换为列表响应，并批量还原接口名称、路径和用户名
    /// </summary>
    /// <param name="logs">当前页日志</param>
    /// <returns>列表响应</returns>
    private async Task<List<ApiRequestLogResult>> ToResultsAsync(List<ApiRequestLog> logs) {
        var crcs = logs.Select(x => x.PathCrc32).Distinct().ToList();
        var apis = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => crcs.Contains(x.PathCrc32))
            .Select(x => new { x.PathCrc32, x.Path, x.Name })
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);

        // CRC32 可能碰撞或同一路径对应多个 HTTP 方法，取第一条即可，名称仅用于展示
        var apiByCrc = apis.GroupBy(x => x.PathCrc32).ToDictionary(x => x.Key, x => x.First());
        var userIds = logs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
        var userNames = await db
            .Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.UserName, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return
        [
            .. logs.Select(log =>
                {
                    _ = apiByCrc.TryGetValue(log.PathCrc32, out var api);
                    var userName = log.UserId.HasValue && userNames.TryGetValue(log.UserId.Value, out var name) ? name : null;
                    return new ApiRequestLogResult(
                        log.Id.ToString(CultureInfo.InvariantCulture), ServerTime.ToOffset(log.CreatedAt), ToMethodName(log.Method), api?.Path
                        , api?.Name
                        , log.PathCrc32, log.StatusCode, log.ApiResponseCode, log.ElapsedMilliseconds
                        , log.UserId?.ToString(CultureInfo.InvariantCulture)
                        , userName, log.DepartmentId?.ToString(CultureInfo.InvariantCulture), ToIpText(log.ClientIp), ToIpText(log.ServerIp)
                        , log.TraceId.ToString(CultureInfo.InvariantCulture), log.WorkerId
                    );
                }
            )
        ];
    }
}