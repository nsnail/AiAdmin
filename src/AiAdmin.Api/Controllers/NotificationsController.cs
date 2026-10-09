using System.Globalization;
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

/// <summary>
///     当前用户消息通知控制器
/// </summary>
/// <param name="db">应用数据库上下文</param>
[ApiController]
[ApiDescription("User notifications")]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     清空当前用户全部通知
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("clear")]
    [ApiDescription("Clear all notifications")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAsync() {
        var userId = GetUserId();
        _ = await db
            .UserMessages.Where(x => x.UserId == userId && !x.IsDeleted)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDeleted, true), HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     删除单条通知
    /// </summary>
    /// <param name="request">通知标识请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete notification")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] NotificationIdRequest request) {
        var item = await FindAsync(request.Id).ConfigureAwait(false);
        _ = item?.IsDeleted = true;

        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     查询当前用户通知并支持分页加载
    /// </summary>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页条数</param>
    /// <returns>通知分页结果</returns>
    [HttpGet]
    [ApiDescription("Query user notifications")]
    public async Task<ActionResult<ApiResponse<UserMessagePageResult>>> ListAsync(
        int current = 1
        , int size = 20
    ) {
        var userId = GetUserId();
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 50);
        var query = db.UserMessages.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted);
        var items = await query
            .OrderByDescending(x => !x.IsRead)
            .ThenByDescending(x => x.Message.IsPopup)
            .ThenByDescending(x => x.Message.CreatedAt)
            .Skip((current - 1) * size)
            .Take(size)
            .Select(x => new UserMessageListItem(
                    x.MessageId, x.Message.Title, x.Message.Content, ServerTime.ToOffset(x.Message.CreatedAt), x.IsRead
                    , db.Users.Where(user => user.Id == x.Message.SenderId).Select(user => user.UserName).FirstOrDefault() ?? string.Empty
                    , db.Users.Where(user => user.Id == x.Message.SenderId).Select(user => user.Avatar).FirstOrDefault(), x.Message.IsPopup
                )
            )
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        var unread = await query.CountAsync(x => !x.IsRead, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<UserMessagePageResult>.Ok(new UserMessagePageResult(items, items.Count == size, unread)));
    }

    /// <summary>
    ///     标记当前用户全部通知为已读
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("read-all")]
    [ApiDescription("Mark all notifications as read")]
    public async Task<ActionResult<ApiResponse<object>>> ReadAllAsync() {
        var userId = GetUserId();
        _ = await db
            .UserMessages.Where(x => x.UserId == userId && !x.IsDeleted && !x.IsRead)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsRead, true), HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     标记单条通知为已读
    /// </summary>
    /// <param name="request">通知标识请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("read")]
    [ApiDescription("Mark notification as read")]
    public async Task<ActionResult<ApiResponse<object>>> ReadAsync([FromBody] NotificationIdRequest request) {
        var item = await FindAsync(request.Id).ConfigureAwait(false);
        _ = item?.IsRead = true;

        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     查找 FindAsync 方法对应的业务数据
    /// </summary>
    /// <param name="id">消息主键</param>
    /// <returns>FindAsync 方法的执行结果</returns>
    private Task<UserMessage?> FindAsync(long id) {
        var userId = GetUserId();
        return db.UserMessages.SingleOrDefaultAsync(x => x.MessageId == id && x.UserId == userId && !x.IsDeleted, HttpContext.RequestAborted);
    }

    /// <summary>
    ///     获取 GetUserId 方法对应的业务数据
    /// </summary>
    /// <returns>GetUserId 方法的执行结果</returns>
    private long GetUserId() {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }
}