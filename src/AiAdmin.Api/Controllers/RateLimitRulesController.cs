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
///     接口限流规则管理控制器，可对全局或指定用户配置滑动窗口、漏桶和令牌桶限流
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="ruleCache">限流规则缓存</param>
[ApiController]
[ApiDescription("Rate limit management")]
[Route("api/rate-limit")]
public sealed class RateLimitRulesController(AppDbContext db, RateLimitRuleCache ruleCache) : ControllerBase
{
    /// <summary>
    ///     列表展示列到实体字段的别名：接口列展示方法和路径，实际按关联接口的路径和方法筛选、排序
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = "ApiEndpoint.Path", ["method"] = "ApiEndpoint.Method", ["apiName"] = "ApiEndpoint.Name"
    };

    /// <summary>
    ///     新增限流规则
    /// </summary>
    /// <param name="request">限流规则保存请求</param>
    /// <returns>新增后的限流规则</returns>
    [HttpPost]
    [ApiDescription("Create rate limit rule")]
    [Idempotent]
    public Task<ActionResult<ApiResponse<RateLimitRuleListItem>>> CreateAsync([FromBody] SaveRateLimitRuleRequest request) {
        return SaveAsync(null, request);
    }

    /// <summary>
    ///     删除限流规则，单条和批量共用
    /// </summary>
    /// <param name="request">包含规则主键集合的请求体</param>
    /// <returns>实际删除的规则数量</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete rate limit rules")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] DeleteRateLimitRuleRequest request) {
        var ids = request.Ids.Distinct().ToList();
        var rows = await db.RateLimitRules.Where(x => ids.Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        if (rows.Count != ids.Count) {
            return NotFound(new ApiResponse<object>(404, "Some rate limit rules do not exist", null));
        }

        db.RateLimitRules.RemoveRange(rows);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { count = rows.Count }, "Rate limit rules deleted"));
    }

    /// <summary>
    ///     导出限流规则，未勾选时导出当前查询条件和排序下的全部结果，受系统设置的导出上限约束
    /// </summary>
    /// <param name="request">动态筛选与排序请求</param>
    /// <param name="limits">导出上限服务</param>
    /// <returns>受系统设置条数限制的限流规则导出结果</returns>
    [HttpPost("export")]
    [ApiDescription("Export rate limit rule data")]
    public async Task<ActionResult<ApiResponse<DynamicExportResult<RateLimitRuleListItem>>>> ExportAsync(
        [FromBody] DynamicExportRequest request
        , [FromServices] ExportLimitService limits
    ) {
        var result = await ListExport
            .RunWithLookupAsync(
                db.RateLimitRules.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _aliases), limits
                , query => ((IOrderedQueryable<RateLimitRule>)query.ApplyDynamicSort(
                        request.SortField, request.SortOrder, nameof(RateLimitRule.CreatedAt), true, _aliases
                    ))
                    .ThenByKey(x => x.Id, request.SortField, request.SortOrder, true)
                    .Select(x => new { Rule = x, x.ApiEndpoint.Method, x.ApiEndpoint.Path, ApiName = x.ApiEndpoint.Name }), async rows =>
                {
                    var users = await db.Users.LoadDisplayInfoAsync(rows.Select(x => x.Rule.UserId)).ConfigureAwait(false);

                    // 参与 Contains 翻译的内存集合使用 List<T>
                    var roleCodes = rows.Where(x => x.Rule.RoleCode is not null).Select(x => x.Rule.RoleCode).Distinct().ToList();
                    var roleNames = roleCodes.Count == 0
                        ? []
                        : await db
                            .Roles.AsNoTracking()
                            .Where(x => roleCodes.Contains(x.Code))
                            .ToDictionaryAsync(x => x.Code, x => x.Name)
                            .ConfigureAwait(false);
                    return rows.ConvertAll(x => ToListItem(
                            x.Rule, x.Method, x.Path, x.ApiName
                            , x.Rule.UserId.HasValue && users.TryGetValue(x.Rule.UserId.Value, out var user) ? user.UserName : null
                            , x.Rule.RoleCode is not null && roleNames.TryGetValue(x.Rule.RoleCode, out var roleName) ? roleName : null
                        )
                    );
                }
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<DynamicExportResult<RateLimitRuleListItem>>.Ok(result));
    }

    /// <summary>
    ///     查询限流规则列表筛选字段元数据
    /// </summary>
    /// <returns>限流规则筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query rate limit filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<RateLimitRule>()));
    }

    /// <summary>
    ///     查询限流规则列表分组统计
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>限流规则列表分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query rate limit filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService
            .GetGroupsAsync(db.RateLimitRules.AsNoTracking(), request.DynamicFilter, _aliases)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询限流规则列表
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>限流规则分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query rate limit rule list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<RateLimitRuleListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.RateLimitRules.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _aliases);
        var total = await query.CountAsync().ConfigureAwait(false);

        // 追加主键作为次级排序，保证排序字段取值相同时分页结果稳定
        var sorted = (IOrderedQueryable<RateLimitRule>)query.ApplyDynamicSort(
            request.SortField, request.SortOrder, nameof(RateLimitRule.CreatedAt), true, _aliases
        );
        var rows = await sorted
            .ThenByKey(x => x.Id, request.SortField, request.SortOrder, true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .Select(x => new { Rule = x, x.ApiEndpoint.Method, x.ApiEndpoint.Path, ApiName = x.ApiEndpoint.Name })
            .ToListAsync()
            .ConfigureAwait(false);
        var users = await db.Users.LoadDisplayInfoAsync(rows.Select(x => x.Rule.UserId)).ConfigureAwait(false);

        // 参与 Contains 翻译的内存集合使用 List<T>
        var roleCodes = rows.Where(x => x.Rule.RoleCode is not null).Select(x => x.Rule.RoleCode).Distinct().ToList();
        var roleNames = roleCodes.Count == 0
            ? []
            : await db.Roles.AsNoTracking().Where(x => roleCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, x => x.Name).ConfigureAwait(false);
        var records = rows.ConvertAll(x => ToListItem(
                x.Rule, x.Method, x.Path, x.ApiName
                , x.Rule.UserId.HasValue && users.TryGetValue(x.Rule.UserId.Value, out var user) ? user.UserName : null
                , x.Rule.RoleCode is not null && roleNames.TryGetValue(x.Rule.RoleCode, out var roleName) ? roleName : null
            )
        );
        return Ok(
            ApiResponse<PagedResponse<RateLimitRuleListItem>>.Ok(
                new PagedResponse<RateLimitRuleListItem>(records, request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     更新限流规则的启用状态
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>状态更新结果</returns>
    [HttpPost("enabled")]
    [ApiDescription("Update rate limit rule enabled state")]
    public async Task<ActionResult<ApiResponse<object>>> SetEnabledAsync([FromBody] SetEnabledRequest request) {
        var enabled = request.IsEnabled;
        var affected = await db
            .RateLimitRules.Where(x => x.Id == request.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, enabled).SetProperty(row => row.UpdatedAt, DateTime.UtcNow))
            .ConfigureAwait(false);
        if (affected == 0) {
            return NotFound(new ApiResponse<object>(404, "Resource not found", null));
        }

        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { request.IsEnabled }, "Enabled state updated"));
    }

    /// <summary>
    ///     修改限流规则
    /// </summary>
    /// <param name="request">限流规则保存请求，必须包含主键</param>
    /// <returns>修改后的限流规则</returns>
    [HttpPost("update")]
    [ApiDescription("Update rate limit rule")]
    public Task<ActionResult<ApiResponse<RateLimitRuleListItem>>> UpdateAsync([FromBody] SaveRateLimitRuleRequest request) {
        return SaveAsync(request.Id, request);
    }

    /// <summary>
    ///     转换限流规则实体为列表项
    /// </summary>
    /// <param name="row">限流规则实体</param>
    /// <param name="method">接口请求方法</param>
    /// <param name="path">接口路径</param>
    /// <param name="apiName">接口名称</param>
    /// <param name="userName">指定的用户名</param>
    /// <param name="roleName">指定的角色名称</param>
    /// <returns>限流规则列表项</returns>
    private static RateLimitRuleListItem ToListItem(
        RateLimitRule row
        , string method
        , string path
        , string apiName
        , string? userName
        , string? roleName
    ) {
        return new RateLimitRuleListItem(
            row.Id, ServerTime.ToOffset(row.CreatedAt), row.ApiEndpointId, method, path, apiName, row.Algorithm, row.Scope, row.RoleCode, roleName
            , row.UserId, userName, row.Limit, row.WindowSeconds, row.IsEnabled, row.Remark
            , row.UpdatedAt.HasValue ? ServerTime.ToOffset(row.UpdatedAt.Value) : null
        );
    }

    /// <summary>
    ///     校验保存请求，通过返回 null，不通过返回对应的 400 结果
    /// </summary>
    /// <param name="request">限流规则保存请求</param>
    /// <returns>校验不通过返回 400 结果，通过返回 null</returns>
    private static BadRequestObjectResult? Validate(SaveRateLimitRuleRequest request) {
        if (!Enum.IsDefined(request.Algorithm) || !Enum.IsDefined(request.Scope)) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "Rate limit option is invalid", null));
        }

        if (request.UserId.HasValue && request.Scope != RateLimitScope.User) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "A specific user requires the user scope", null));
        }

        return request.Scope == RateLimitScope.Role && string.IsNullOrWhiteSpace(request.RoleCode)
            ? new BadRequestObjectResult(new ApiResponse<object>(400, "Role is required for the role scope", null))
            : null;
    }

    /// <summary>
    ///     新增或修改限流规则的公共流程
    /// </summary>
    /// <param name="id">规则主键，新增时为空</param>
    /// <param name="request">限流规则保存请求</param>
    /// <returns>保存后的限流规则</returns>
    private async Task<ActionResult<ApiResponse<RateLimitRuleListItem>>> SaveAsync(
        long? id
        , SaveRateLimitRuleRequest request
    ) {
        var invalid = Validate(request);
        if (invalid is not null) {
            return invalid;
        }

        var endpoint = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => x.Id == request.ApiEndpointId)
            .Select(x => new { x.Method, x.Path, x.Name })
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (endpoint is null) {
            return NotFound(new ApiResponse<object>(404, "API does not exist", null));
        }

        string? userName = null;
        if (request.UserId.HasValue) {
            userName = await db
                .Users.AsNoTracking()
                .Where(x => x.Id == request.UserId.Value)
                .Select(x => x.UserName)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);
            if (userName is null) {
                return NotFound(new ApiResponse<object>(404, "User not found", null));
            }
        }

        string? roleName = null;
        var roleCode = request.Scope == RateLimitScope.Role ? request.RoleCode!.Trim() : null;
        if (roleCode is not null) {
            roleName = await db.Roles.AsNoTracking().Where(x => x.Code == roleCode).Select(x => x.Name).SingleOrDefaultAsync().ConfigureAwait(false);
            if (roleName is null) {
                return NotFound(new ApiResponse<object>(404, "Role not found", null));
            }
        }

        var row = id.HasValue ? await db.RateLimitRules.FindAsync(id.Value).ConfigureAwait(false) : new RateLimitRule();
        if (row is null) {
            return NotFound(new ApiResponse<object>(404, "Rate limit rule does not exist", null));
        }

        row.ApiEndpointId = request.ApiEndpointId;
        row.Algorithm = request.Algorithm;
        row.Scope = request.Scope;
        row.UserId = request.Scope == RateLimitScope.User ? request.UserId : null;
        row.RoleCode = roleCode;
        row.Limit = request.Limit;
        row.WindowSeconds = request.WindowSeconds;
        row.IsEnabled = request.IsEnabled;
        row.Remark = request.Remark.Trim();
        if (id.HasValue) {
            row.UpdatedAt = DateTime.UtcNow;
        }
        else {
            _ = await db.RateLimitRules.AddAsync(row).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await ruleCache.InvalidateAsync().ConfigureAwait(false);
        return Ok(
            ApiResponse<RateLimitRuleListItem>.Ok(
                ToListItem(row, endpoint.Method, endpoint.Path, endpoint.Name, userName, roleName)
                , id.HasValue ? "Rate limit rule updated" : "Rate limit rule created"
            )
        );
    }
}