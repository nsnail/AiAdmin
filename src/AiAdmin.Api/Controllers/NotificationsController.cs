using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;
#pragma warning disable IDE0031

/// <summary>
///     当前用户消息通知控制器
/// </summary>
[ApiController]
[ApiDescription("User notifications")]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     查询当前用户通知并支持分页加载
    /// </summary>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页条数</param>
    /// <returns>通知分页结果</returns>
    [HttpGet]
    [ApiDescription("Query user notifications")]
    public async Task<ActionResult<ApiResponse<UserMessagePageResult>>> ListAsync(int current = 1, int size = 20) {
        var userId = GetUserId();
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 50);
        var query = db.UserMessages.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted);
        var items = await query.OrderByDescending(x => x.Message.CreatedAt).Skip((current - 1) * size).Take(size)
            .Select(x => new UserMessageListItem(x.MessageId, x.Message.Title, x.Message.Content, ServerTime.ToOffset(x.Message.CreatedAt), x.IsRead)).ToListAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var unread = await query.CountAsync(x => !x.IsRead, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<UserMessagePageResult>.Ok(new UserMessagePageResult(items, items.Count == size, unread)));
    }

    /// <summary>标记单条通知为已读</summary>
    /// <param name="id">消息主键</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id:long}/read")]
    [ApiDescription("Mark notification as read")]
    public async Task<ActionResult<ApiResponse<object>>> ReadAsync(long id) {
        var item = await FindAsync(id).ConfigureAwait(false);
        if (item is not null) {
            item.IsRead = true;
        }

        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>标记当前用户全部通知为已读</summary>
    /// <returns>操作结果</returns>
    [HttpPut("read-all")]
    [ApiDescription("Mark all notifications as read")]
    public async Task<ActionResult<ApiResponse<object>>> ReadAllAsync() {
        _ = await db.UserMessages.Where(x => x.UserId == GetUserId() && !x.IsDeleted && !x.IsRead).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsRead, true), HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>删除单条通知</summary>
    /// <param name="id">消息主键</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete notification")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var item = await FindAsync(id).ConfigureAwait(false);
        if (item is not null) {
            item.IsDeleted = true;
        }

        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>清空当前用户全部通知</summary>
    /// <returns>操作结果</returns>
    [HttpDelete]
    [ApiDescription("Clear all notifications")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAsync() {
        _ = await db.UserMessages.Where(x => x.UserId == GetUserId() && !x.IsDeleted).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDeleted, true), HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private long GetUserId() {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private Task<UserMessage?> FindAsync(long id) {
        return db.UserMessages.SingleOrDefaultAsync(x => x.MessageId == id && x.UserId == GetUserId() && !x.IsDeleted, HttpContext.RequestAborted);
    }
}