using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理系统资源的启用状态
/// </summary>
[ApiController]
[ApiDescription("Enabled state management")]
[Authorize]
[Route("api/enabled-state")]
public sealed class EnabledStatesController(AppDbContext db, ApiPermissionCache permissionCache) : ControllerBase
{
    /// <summary>
    ///     更新指定资源的启用状态
    /// </summary>
    /// <param name="resource">资源类型</param>
    /// <param name="id">记录主键</param>
    /// <param name="request">启用状态请求</param>
    /// <returns>状态更新结果</returns>
    [HttpPut("{resource}/{id:long}")]
    [ApiDescription("Update resource enabled state")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAsync(
        string resource
        , long id
        , UpdateEnabledRequest request
    ) {
        var affected = resource switch
        {
            "user" => await db
                .Users.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "role" => await db
                .Roles.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "menu" => await db
                .Menus.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "department" => await db
                .Departments.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , "dictionary-item" => await db
                .DictionaryItems.Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsEnabled, request.IsEnabled))
                .ConfigureAwait(false)
            , _ => -1
        };
        switch (affected) {
            case < 0:
                return BadRequest(new ApiResponse<object>(400, "Unsupported enabled state resource", null));
            case 0:
                return NotFound(new ApiResponse<object>(404, "Resource not found", null));
        }

        if (resource is "role" or "menu") {
            permissionCache.Invalidate();
        }

        return Ok(ApiResponse<object>.Ok(new { request.IsEnabled }, "Enabled state updated"));
    }
}