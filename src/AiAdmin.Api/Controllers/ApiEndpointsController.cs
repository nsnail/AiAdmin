using System.Diagnostics.CodeAnalysis;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 提供接口列表、反射同步及匿名访问开关管理。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理系统接口及其授权配置
/// </summary>
[ApiController]
[ApiDescription("API management")]
[Authorize]
[Route("api/api-endpoint")]
[SuppressMessage("Design", "S6960:Controllers should not have too many responsibilities", Justification = "接口同步和匿名配置共同属于接口管理边界。")]
public sealed class ApiEndpointsController(AppDbContext db, ApiEndpointSyncService syncService) : ControllerBase
{
    /// <summary>
    ///     查询系统接口列表
    /// </summary>
    /// <returns>接口列表响应</returns>
    [HttpGet("list")]
    [ApiDescription("Query API list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiEndpointResult>>>> ListAsync() {
        // 返回按请求语言转换后的接口和控制器名称。
        var endpoints = await db
            .ApiEndpoints.AsNoTracking()
            .OrderBy(x => x.Controller)
            .ThenBy(x => x.Path)
            .ThenBy(x => x.Method)
            .Select(x => new ApiEndpointResult(x.Id, x.Name, x.AllowAnonymous, x.Method, x.Path, x.Controller, x.ControllerName, x.Action))
            .ToListAsync()
            .ConfigureAwait(false);
        endpoints =
        [
            .. endpoints.Select(x => x with
                {
                    Name = ApiMessages.GetApiDescription(Request, x.Name)
                    , ControllerName = ApiMessages.GetApiDescription(Request, x.ControllerName)
                }
            )
        ];
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

    /// <summary>
    ///     更新接口是否允许匿名访问
    /// </summary>
    /// <param name="id">接口主键</param>
    /// <param name="request">匿名访问配置</param>
    /// <returns>更新结果</returns>
    [HttpPut("{id:long}/anonymous")]
    [ApiDescription("Update API anonymous access")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAnonymousAsync(
        long id
        , UpdateApiAnonymousRequest request
    ) {
        var endpoint = await db.ApiEndpoints.FindAsync([id], HttpContext.RequestAborted).ConfigureAwait(false);
        if (endpoint is null) {
            return NotFound(new ApiResponse<object>(404, "API endpoint not found", null));
        }

        endpoint.AllowAnonymous = request.AllowAnonymous;
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "API anonymous access updated"));
    }
}