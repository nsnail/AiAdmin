using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Filters;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     接口熔断规则管理控制器：窗口内失败率达到阈值时暂停放行故障接口，到期后以少量试探请求探测恢复
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="ruleCache">熔断规则缓存</param>
/// <param name="breaker">熔断器</param>
[ApiController]
[ApiDescription("Circuit breaker management")]
[Route("api/circuit-breaker")]
public sealed class CircuitBreakerRulesController(AppDbContext db, CircuitBreakerRuleCache ruleCache, CircuitBreaker breaker) : ControllerBase
{
    /// <summary>
    ///     列表展示列到实体字段的别名：接口列展示方法和路径，实际按关联接口的路径和方法筛选、排序
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = "ApiEndpoint.Path", ["method"] = "ApiEndpoint.Method", ["apiName"] = "ApiEndpoint.Name"
    };

    /// <summary>
    ///     新增熔断规则
    /// </summary>
    /// <param name="request">熔断规则保存请求</param>
    /// <returns>新增后的熔断规则</returns>
    [HttpPost]
    [ApiDescription("Create circuit breaker rule")]
    [Idempotent]
    public Task<ActionResult<ApiResponse<CircuitBreakerRuleListItem>>> CreateAsync([FromBody] SaveCircuitBreakerRuleRequest request) {
        return SaveAsync(null, request);
    }

    /// <summary>
    ///     删除熔断规则，单条和批量共用
    /// </summary>
    /// <param name="request">包含规则主键集合的请求体</param>
    /// <returns>实际删除的规则数量</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete circuit breaker rules")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] DeleteCircuitBreakerRuleRequest request) {
        var ids = request.Ids.Distinct().ToList();
        var rows = await db.CircuitBreakerRules.Where(x => ids.Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        if (rows.Count != ids.Count) {
            return NotFound(new ApiResponse<object>(404, "Some circuit breaker rules do not exist", null));
        }

        db.CircuitBreakerRules.RemoveRange(rows);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { count = rows.Count }, "Circuit breaker rules deleted"));
    }

    /// <summary>
    ///     导出熔断规则，未勾选时导出当前查询条件和排序下的全部结果，受系统设置的导出上限约束
    /// </summary>
    /// <param name="request">动态筛选与排序请求</param>
    /// <param name="limits">导出上限服务</param>
    /// <returns>受系统设置条数限制的熔断规则导出结果</returns>
    [HttpPost("export")]
    [ApiDescription("Export circuit breaker rule data")]
    public async Task<ActionResult<ApiResponse<DynamicExportResult<CircuitBreakerRuleListItem>>>> ExportAsync(
        [FromBody] DynamicExportRequest request
        , [FromServices] ExportLimitService limits
    ) {
        var result = await ListExport
            .RunWithLookupAsync(
                db.CircuitBreakerRules.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _aliases), limits
                , query => ((IOrderedQueryable<CircuitBreakerRule>)query.ApplyDynamicSort(
                        request.SortField, request.SortOrder, nameof(CircuitBreakerRule.CreatedAt), true, _aliases
                    ))
                    .ThenByKey(x => x.Id, request.SortField, request.SortOrder, true)
                    .Select(x => new { Rule = x, x.ApiEndpoint.Method, x.ApiEndpoint.Path, ApiName = x.ApiEndpoint.Name }), async rows =>
                {
                    var records = new List<CircuitBreakerRuleListItem>(rows.Count);
                    foreach (var row in rows) {
                        records.Add(await ToListItemAsync(row.Rule, row.Method, row.Path, row.ApiName).ConfigureAwait(false));
                    }

                    return records;
                }
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<DynamicExportResult<CircuitBreakerRuleListItem>>.Ok(result));
    }

    /// <summary>
    ///     查询熔断规则列表筛选字段元数据
    /// </summary>
    /// <returns>熔断规则筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query circuit breaker filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<CircuitBreakerRule>()));
    }

    /// <summary>
    ///     查询熔断规则列表分组统计
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>熔断规则列表分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query circuit breaker filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService
            .GetGroupsAsync(db.CircuitBreakerRules.AsNoTracking(), request.DynamicFilter, _aliases)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询熔断规则列表，并附带每条规则当前的熔断状态
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>熔断规则分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query circuit breaker rule list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<CircuitBreakerRuleListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.CircuitBreakerRules.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _aliases);
        var total = await query.CountAsync().ConfigureAwait(false);

        // 追加主键作为次级排序，保证排序字段取值相同时分页结果稳定
        var sorted = (IOrderedQueryable<CircuitBreakerRule>)query.ApplyDynamicSort(
            request.SortField, request.SortOrder, nameof(CircuitBreakerRule.CreatedAt), true, _aliases
        );
        var rows = await sorted
            .ThenByKey(x => x.Id, request.SortField, request.SortOrder, true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .Select(x => new { Rule = x, x.ApiEndpoint.Method, x.ApiEndpoint.Path, ApiName = x.ApiEndpoint.Name })
            .ToListAsync()
            .ConfigureAwait(false);
        var records = new List<CircuitBreakerRuleListItem>(rows.Count);
        foreach (var row in rows) {
            records.Add(await ToListItemAsync(row.Rule, row.Method, row.Path, row.ApiName).ConfigureAwait(false));
        }

        return Ok(
            ApiResponse<PagedResponse<CircuitBreakerRuleListItem>>.Ok(
                new PagedResponse<CircuitBreakerRuleListItem>(records, request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     更新熔断规则的启用状态
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>状态更新结果</returns>
    [HttpPost("enabled")]
    [ApiDescription("Update circuit breaker rule enabled state")]
    public async Task<ActionResult<ApiResponse<object>>> SetEnabledAsync([FromBody] SetEnabledRequest request) {
        var enabled = request.IsEnabled;
        var affected = await db
            .CircuitBreakerRules.Where(x => x.Id == request.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, enabled).SetProperty(row => row.UpdatedAt, DateTime.UtcNow))
            .ConfigureAwait(false);
        if (affected == 0) {
            return NotFound(new ApiResponse<object>(404, "Resource not found", null));
        }

        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { request.IsEnabled }, "Enabled state updated"));
    }

    /// <summary>
    ///     修改熔断规则
    /// </summary>
    /// <param name="request">熔断规则保存请求，必须包含主键</param>
    /// <returns>修改后的熔断规则</returns>
    [HttpPost("update")]
    [ApiDescription("Update circuit breaker rule")]
    public Task<ActionResult<ApiResponse<CircuitBreakerRuleListItem>>> UpdateAsync([FromBody] SaveCircuitBreakerRuleRequest request) {
        return SaveAsync(request.Id, request);
    }

    /// <summary>
    ///     新增或修改熔断规则的公共流程
    /// </summary>
    /// <param name="id">规则主键，新增时为空</param>
    /// <param name="request">熔断规则保存请求</param>
    /// <returns>保存后的熔断规则</returns>
    private async Task<ActionResult<ApiResponse<CircuitBreakerRuleListItem>>> SaveAsync(
        long? id
        , SaveCircuitBreakerRuleRequest request
    ) {
        var endpoint = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => x.Id == request.ApiEndpointId)
            .Select(x => new { x.Method, x.Path, x.Name })
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (endpoint is null) {
            return NotFound(new ApiResponse<object>(404, "API does not exist", null));
        }

        // 每个接口最多一条熔断规则
        if (await db.CircuitBreakerRules.AnyAsync(x => x.ApiEndpointId == request.ApiEndpointId && x.Id != (id ?? 0)).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "The API already has a circuit breaker rule", null));
        }

        var row = id.HasValue ? await db.CircuitBreakerRules.FindAsync(id.Value).ConfigureAwait(false) : new CircuitBreakerRule();
        if (row is null) {
            return NotFound(new ApiResponse<object>(404, "Circuit breaker rule does not exist", null));
        }

        row.ApiEndpointId = request.ApiEndpointId;
        row.FailureRatePercent = request.FailureRatePercent;
        row.MinimumRequests = request.MinimumRequests;
        row.WindowSeconds = request.WindowSeconds;
        row.OpenSeconds = request.OpenSeconds;
        row.HalfOpenRequests = request.HalfOpenRequests;
        row.SlowCallMs = request.SlowCallMs;
        row.IsEnabled = request.IsEnabled;
        row.Remark = request.Remark.Trim();
        if (id.HasValue) {
            row.UpdatedAt = DateTime.UtcNow;
        }
        else {
            _ = await db.CircuitBreakerRules.AddAsync(row).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(
            ApiResponse<CircuitBreakerRuleListItem>.Ok(
                await ToListItemAsync(row, endpoint.Method, endpoint.Path, endpoint.Name).ConfigureAwait(false)
                , id.HasValue ? "Circuit breaker rule updated" : "Circuit breaker rule created"
            )
        );
    }

    /// <summary>
    ///     转换熔断规则实体为列表项，已启用的规则从 Redis 读取当前熔断状态，读取失败时按关闭展示
    /// </summary>
    /// <param name="row">熔断规则实体</param>
    /// <param name="method">接口请求方法</param>
    /// <param name="path">接口路径</param>
    /// <param name="apiName">接口名称</param>
    /// <returns>熔断规则列表项</returns>
    private async Task<CircuitBreakerRuleListItem> ToListItemAsync(
        CircuitBreakerRule row
        , string method
        , string path
        , string apiName
    ) {
        var state = CircuitBreakerState.Closed;
        var retryAfter = 0;
        if (row.IsEnabled) {
            try {
                var snapshot = new CircuitBreakerRuleSnapshot(
                    row.Id, row.FailureRatePercent, row.MinimumRequests, row.WindowSeconds, row.OpenSeconds, row.HalfOpenRequests, row.SlowCallMs
                );
                (state, retryAfter) = await breaker.GetStateAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception) {
                // 状态读取失败不影响规则列表，按关闭展示
                state = CircuitBreakerState.Closed;
            }
        }

        return new CircuitBreakerRuleListItem(
            row.Id, ServerTime.ToOffset(row.CreatedAt), row.ApiEndpointId, method, path, apiName, row.FailureRatePercent, row.MinimumRequests
            , row.WindowSeconds, row.OpenSeconds, row.HalfOpenRequests, row.SlowCallMs, row.IsEnabled, row.Remark
            , row.UpdatedAt.HasValue ? ServerTime.ToOffset(row.UpdatedAt.Value) : null, state, retryAfter
        );
    }
}