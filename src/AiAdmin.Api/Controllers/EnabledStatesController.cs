using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理系统资源的启用状态
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="permissionCache">接口权限缓存</param>
/// <param name="dataScopeCache">数据权限缓存</param>
[ApiController]
[ApiDescription("Enabled state management")]
[Route("api/enabled-state")]
public sealed partial class EnabledStatesController(AppDbContext db, ApiPermissionCache permissionCache, DataScopeCache dataScopeCache)
    : ControllerBase
{
    /// <summary>
    ///     更新指定资源的启用状态
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>状态更新结果</returns>
    [HttpPost]
    [ApiDescription("Update resource enabled state")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAsync([FromBody] ResourceEnabledRequest request) {
        if (IsStringKeyResource(request.Resource) ? string.IsNullOrWhiteSpace(request.Key) : request.Id <= 0) {
            return BadRequest(new ApiResponse<object>(400, "Resource id is required", null));
        }

        var affected = request.Resource switch
        {
            "user" => await db
                .Users.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "role" => await db
                .Roles.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "menu" => await db
                .Menus.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "department" => await db
                .Departments.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "dictionary-item" => await db
                .DictionaryItems.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "scheduled-job" => await db
                .ScheduledJobs.Where(x => x.Id == request.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , _ => await UpdateBusinessEnabledStateAsync(request).ConfigureAwait(false)
        };
        switch (affected) {
            case < 0:
                return BadRequest(new ApiResponse<object>(400, "Unsupported enabled state resource", null));
            case 0:
                return NotFound(new ApiResponse<object>(404, "Resource not found", null));
        }

        if (request.Resource is "role" or "department" or "menu") {
            permissionCache.Invalidate();
        }

        if (request.Resource is "role" or "department") {
            dataScopeCache.InvalidateInBackground();
        }

        return Ok(ApiResponse<object>.Ok(new { request.IsEnabled }, "Enabled state updated"));
    }

    /// <summary>
    ///     判断资源是否使用字符串主键，由 EnabledStatesController.Business.cs 实现
    /// </summary>
    /// <param name="resource">资源类型</param>
    /// <returns>使用字符串主键时为 true</returns>
    private static partial bool IsStringKeyResource(string resource);

    /// <summary>
    ///     更新框架之外的业务资源的启用状态，由 EnabledStatesController.Business.cs 实现
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>受影响的行数，资源类型不受支持时为 -1</returns>
    private static partial Task<int> UpdateBusinessEnabledStateAsync(ResourceEnabledRequest request);
}