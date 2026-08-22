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
///     管理员系统消息管理控制器
/// </summary>
[ApiController]
[ApiDescription("System message management")]
[Authorize(Roles = "R_SUPER,R_ADMIN")]
[Route("api/message")]
public sealed class MessagesController(AppDbContext db) : ControllerBase
{
    /// <summary>查询消息列表筛选字段元数据</summary>
    /// <returns>筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query message filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<SystemMessage>()));
    }

    /// <summary>
    ///     查询管理员已发送的消息
    /// </summary>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页条数</param>
    /// <param name="keyword">标题关键字</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>消息列表</returns>
    [HttpGet("list")]
    [ApiDescription("Query sent messages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SystemMessageListItem>>>> ListAsync(int current = 1, int size = 20, string? keyword = null, DateTime? startTime = null, DateTime? endTime = null) {
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 100);
        var query = db.SystemMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword)) {
            query = query.Where(x => x.Title.Contains(keyword));
        }

        if (startTime.HasValue) {
            query = query.Where(x => x.CreatedAt >= startTime.Value);
        }

        if (endTime.HasValue) {
            query = query.Where(x => x.CreatedAt < endTime.Value);
        }

        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((current - 1) * size).Take(size)
            .Select(x => new SystemMessageListItem(x.Id, x.Title, x.Content, ServerTime.ToOffset(x.CreatedAt), x.Recipients.Count)).ToListAsync().ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<SystemMessageListItem>>.Ok(items));
    }

    /// <summary>
    ///     向指定用户发送系统消息
    /// </summary>
    /// <param name="request">消息和发送对象</param>
    /// <returns>发送结果</returns>
    [HttpPost]
    [ApiDescription("Send system message")]
    public async Task<ActionResult<ApiResponse<object>>> SendAsync(SendSystemMessageRequest request) {
        var targetType = request.TargetType.Trim().ToLowerInvariant();
        if (targetType is not ("all" or "department" or "department_children" or "user")) {
            return BadRequest(new ApiResponse<object>(400, "Invalid message recipient type", null));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content)) {
            return BadRequest(new ApiResponse<object>(400, "Message title and content are required", null));
        }

        var userIds = await ResolveUserIdsAsync(targetType, request.DepartmentIds, request.UserIds).ConfigureAwait(false);
        if (userIds.Count == 0) {
            return BadRequest(new ApiResponse<object>(400, "No enabled users match the selected recipients", null));
        }

        var senderId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
        var message = new SystemMessage { SenderId = senderId, Title = request.Title.Trim(), Content = request.Content };
        foreach (var userId in userIds) {
            message.Recipients.Add(new UserMessage { UserId = userId, Message = message });
        }

        _ = await db.SystemMessages.AddAsync(message, HttpContext.RequestAborted).ConfigureAwait(false);
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "System message sent"));
    }

    /// <summary>修改已发送消息的标题和正文</summary>
    /// <param name="id">消息主键</param>
    /// <param name="request">修改内容</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update system message")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAsync(long id, UpdateSystemMessageRequest request) {
        var message = await db.SystemMessages.SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted).ConfigureAwait(false);
        if (message is null) {
            return NotFound(new ApiResponse<object>(404, "Message not found", null));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content)) {
            return BadRequest(new ApiResponse<object>(400, "Message title and content are required", null));
        }

        message.Title = request.Title.Trim();
        message.Content = request.Content;
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "System message updated"));
    }

    /// <summary>删除一条系统消息</summary>
    /// <param name="id">消息主键</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete system message")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var message = await db.SystemMessages.SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted).ConfigureAwait(false);
        if (message is null) {
            return NotFound(new ApiResponse<object>(404, "Message not found", null));
        }

        _ = db.SystemMessages.Remove(message);
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "System message deleted"));
    }

    /// <summary>批量删除系统消息</summary>
    /// <param name="ids">消息主键集合</param>
    /// <returns>操作结果</returns>
    [HttpDelete]
    [ApiDescription("Batch delete system messages")]
    public async Task<ActionResult<ApiResponse<object>>> BatchDeleteAsync([FromBody] long[] ids) {
        if (ids.Length == 0) {
            return Ok(ApiResponse<object>.Ok(new { }));
        }

        var messages = await db.SystemMessages.Where(x => ids.Contains(x.Id)).ToListAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        db.SystemMessages.RemoveRange(messages);
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "System messages deleted"));
    }

    private async Task<HashSet<long>> ResolveUserIdsAsync(string targetType, long[] departmentIds, long[] userIds) {
        if (targetType == "all") {
            return await db.Users.AsNoTracking().Where(x => x.IsEnabled).Select(x => x.Id).ToHashSetAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        }

        if (targetType == "user") {
            return await db.Users.AsNoTracking().Where(x => x.IsEnabled && userIds.Contains(x.Id)).Select(x => x.Id).ToHashSetAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        }

        var selected = departmentIds.Distinct().ToHashSet();
        if (targetType == "department_children") {
            var departments = await db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToListAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            var changed = true;
            while (changed) {
                changed = false;
                foreach (var item in departments.Where(x => x.ParentId.HasValue && selected.Contains(x.ParentId.Value))) {
                    changed |= selected.Add(item.Id);
                }
            }
        }

        return await db.UserDepartments.AsNoTracking().Where(x => selected.Contains(x.DepartmentId) && x.User.IsEnabled).Select(x => x.UserId).ToHashSetAsync(HttpContext.RequestAborted).ConfigureAwait(false);
    }
}