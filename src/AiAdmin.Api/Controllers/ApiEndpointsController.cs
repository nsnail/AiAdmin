using System.Diagnostics.CodeAnalysis;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 提供接口列表和反射同步管理。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理系统接口及其授权配置
/// </summary>
[ApiController]
[ApiDescription("API management")]
[Authorize]
[Route("api/api-endpoint")]
[SuppressMessage("Design", "S6960:Controllers should not have too many responsibilities", Justification = "接口查询和同步共同属于接口管理边界。")]
public sealed class ApiEndpointsController(AppDbContext db, ApiEndpointSyncService syncService) : ControllerBase
{
    /// <summary>
    ///     查询接口列表筛选字段元数据
    /// </summary>
    /// <returns>接口筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query API filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<ApiEndpoint>()));
    }

    /// <summary>
    ///     查询系统接口列表
    /// </summary>
    /// <param name="request">包含动态筛选信息的请求体</param>
    /// <returns>接口列表响应</returns>
    [HttpPost("list")]
    [ApiDescription("Query API list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiEndpointResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        // 返回按请求语言转换后的接口和控制器名称。
        var endpoints = await db
            .ApiEndpoints.AsNoTracking()
            .ApplyDynamicFilter(request.DynamicFilter)
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(ApiEndpoint.Controller))
            .Select(x => new ApiEndpointResult(
                    x.Id, ServerTime.ToOffset(x.CreatedAt), x.Name, x.AllowAnonymous, x.Method, x.Path, x.Controller, x.ControllerName, x.Action
                )
            )
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ApiEndpointResult>>.Ok(endpoints));
    }

    /// <summary>
    ///     反射并同步系统接口
    /// </summary>
    /// <returns>同步统计响应</returns>
    [HttpPost("sync")]
    [ApiDescription("Synchronize system APIs")]
    public async Task<ActionResult<ApiResponse<ApiSyncResult>>> SyncAsync() {
        // 手动触发接口反射同步并刷新权限缓存。
        var result = await syncService.SyncAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<ApiSyncResult>.Ok(result, "API synchronization completed"));
    }
}