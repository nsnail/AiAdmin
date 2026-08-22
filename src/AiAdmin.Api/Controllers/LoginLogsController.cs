using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     登录日志管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/login-log")]
[ApiDescription("Login log management")]
public sealed class LoginLogsController(AppDbContext db) : ControllerBase
{
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
            , entity.OperatingSystem, entity.Browser
            , entity.DeviceType, entity.Platform, entity.Language, entity.TimeZone, entity.ScreenResolution, entity.ViewportSize, entity.ColorDepth
            , entity.PixelRatio, entity.TouchPoints, entity.ClientHints, ServerTime.ToOffset(entity.CreatedAt)
        );
    }
}