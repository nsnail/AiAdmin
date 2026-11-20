using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     登录日志管理控制器
/// </summary>
/// <param name="db">应用数据库上下文</param>
[ApiController]
[Route("api/login-log")]
[ApiDescription("Login log management")]
public sealed class LoginLogsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     导出登录日志，未勾选时导出当前查询条件和排序下的全部结果，受系统设置的导出上限约束
    /// </summary>
    /// <param name="request">动态筛选与排序请求</param>
    /// <param name="limits">导出上限服务</param>
    /// <returns>受系统设置条数限制的登录日志导出结果</returns>
    [HttpPost("export")]
    [ApiDescription("Export login log data")]
    public async Task<ActionResult<ApiResponse<DynamicExportResult<LoginLogResult>>>> ExportAsync(
        [FromBody] DynamicExportRequest request
        , [FromServices] ExportLimitService limits
    ) {
        var result = await ListExport
            .RunAsync(
                db.LoginLogs.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter), limits
                , query => query.ApplyDynamicSort(request.SortField, request.SortOrder, nameof(LoginLog.CreatedAt), true)
                , rows => rows.ConvertAll(ToResult)
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<DynamicExportResult<LoginLogResult>>.Ok(result));
    }

    /// <summary>
    ///     查询登录日志列表筛选字段元数据
    /// </summary>
    /// <returns>登录日志筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query login log filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<LoginLog>()));
    }

    /// <summary>
    ///     查询当前登录日志筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query login log filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService.GetGroupsAsync(db.LoginLogs.AsNoTracking(), request.DynamicFilter).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询登录日志
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>登录日志分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query login log list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<LoginLogResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.LoginLogs.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(LoginLog.CreatedAt), true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<LoginLogResult>>.Ok(
                new PagedResponse<LoginLogResult>(rows.ConvertAll(ToResult), request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     将登录日志实体转换为列表响应
    /// </summary>
    /// <param name="entity">登录日志实体</param>
    /// <returns>登录日志列表响应</returns>
    private static LoginLogResult ToResult(LoginLog entity) {
        return new LoginLogResult(
            entity.Id, entity.UserId, entity.UserName, entity.OwnerId, entity.OwnerDepartmentId, entity.ClientIp, entity.Region, entity.UserAgent
            , entity.OperatingSystem, entity.Browser, entity.DeviceType, entity.Platform, entity.Language, entity.TimeZone, entity.ScreenResolution
            , entity.ViewportSize, entity.ColorDepth, entity.PixelRatio, entity.TouchPoints, entity.ClientHints, ServerTime.ToOffset(entity.CreatedAt)
        );
    }
}