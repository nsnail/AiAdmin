using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     管理当前用户按页面保存的查询条件
/// </summary>
[ApiController]
[ApiDescription("Saved query management")]
[Authorize]
[Route("api/saved-query")]
public sealed class SavedQueriesController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     删除当前用户的查询条件
    /// </summary>
    /// <param name="id">查询条件主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete saved query condition")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var userId = GetCurrentUserId();
        var entity = await db.SavedQueries.SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (entity is null) {
            return NotFound(new ApiResponse<object>(404, "Saved query condition not found", null));
        }

        if ((entity.IsGlobal && !User.IsInRole("R_SUPER")) || (!entity.IsGlobal && entity.UserId != userId)) {
            return Forbid();
        }

        _ = db.SavedQueries.Remove(entity);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Saved query condition deleted"));
    }

    /// <summary>
    ///     查询当前页面已保存的查询条件
    /// </summary>
    /// <param name="route">当前页面路由</param>
    /// <returns>已保存查询条件列表</returns>
    [HttpGet]
    [ApiDescription("Query saved query conditions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SavedQueryResult>>>> ListAsync([FromQuery] [Required] [StringLength(300)] string route) {
        var userId = GetCurrentUserId();
        var queries = await db
            .SavedQueries.AsNoTracking()
            .Where(x => x.Route == route && (x.IsGlobal || x.UserId == userId))
            .OrderBy(x => x.IsGlobal)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
        var result = queries.ConvertAll(x => new SavedQueryResult(
                x.Id, x.Name, x.IsGlobal, JsonSerializer.Deserialize<DynamicFilter>(x.FilterJson, _jsonOptions)!
            )
        );
        return Ok(ApiResponse<IReadOnlyList<SavedQueryResult>>.Ok(result));
    }

    /// <summary>
    ///     保存或更新当前页面的命名查询条件
    /// </summary>
    /// <param name="request">查询条件保存请求</param>
    /// <returns>保存后的查询条件</returns>
    [HttpPost]
    [ApiDescription("Save query condition")]
    public async Task<ActionResult<ApiResponse<SavedQueryResult>>> SaveAsync(SaveQueryRequest request) {
        var userId = GetCurrentUserId();
        var name = request.Name.Trim();
        var route = request.Route.Trim();
        var isSuperAdmin = User.IsInRole("R_SUPER");

        // 全局查询会对所有用户可见，服务端必须独立校验超管身份，不能依赖前端隐藏开关
        if (request.IsGlobal && !isSuperAdmin) {
            return Forbid();
        }

        var isGlobal = request.IsGlobal;
        var filterJson = JsonSerializer.Serialize(request.DynamicFilter, _jsonOptions);
        var entity = await db.SavedQueries.SingleOrDefaultAsync(
            x => x.Route == route && x.Name == name && (isGlobal ? x.IsGlobal : x.UserId == userId && !x.IsGlobal)
        ).ConfigureAwait(false);
        if (entity is null) {
            entity = new SavedQuery { UserId = userId, Route = route, Name = name, IsGlobal = isGlobal, FilterJson = filterJson };
            _ = await db.SavedQueries.AddAsync(entity).ConfigureAwait(false);
        }
        else {
            entity.FilterJson = filterJson;
            entity.IsGlobal = isGlobal;
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<SavedQueryResult>.Ok(new SavedQueryResult(entity.Id, entity.Name, entity.IsGlobal, request.DynamicFilter!)));
    }

    /// <summary>
    ///     获取当前登录用户主键
    /// </summary>
    /// <returns>当前登录用户主键</returns>
    private long GetCurrentUserId() {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }
}