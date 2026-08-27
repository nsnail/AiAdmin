using System.Collections;
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
///     管理员系统消息管理控制器
/// </summary>
/// <param name="db">应用数据库上下文</param>
[ApiController]
[ApiDescription("System message management")]
[Authorize(Roles = "R_SUPER,R_ADMIN")]
[Route("api/message")]
public sealed class MessagesController(AppDbContext db) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> _listAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = nameof(SystemMessage.Id)
        , ["createdAt"] = nameof(SystemMessage.CreatedAt)
        , ["updatedAt"] = nameof(SystemMessage.UpdatedAt)
        , ["title"] = nameof(SystemMessage.Title)
        , ["isPopup"] = nameof(SystemMessage.IsPopup)
        , ["recipientCount"] = $"{nameof(SystemMessage.Recipients)}.{nameof(ICollection.Count)}"
    };

    /// <summary>批量删除系统消息</summary>
    /// <param name="ids">消息主键集合</param>
    /// <returns>操作结果</returns>
    [HttpPost("delete")]
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

    /// <summary>删除一条系统消息</summary>
    /// <param name="id">消息主键</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:long}/delete")]
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

    /// <summary>查询消息列表筛选字段元数据</summary>
    /// <returns>筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query message filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<SystemMessage>()));
    }

    /// <summary>
    ///     查询当前消息筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query message filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService
            .GetGroupsAsync(db.SystemMessages.AsNoTracking(), request.DynamicFilter, _listAliases)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     查询管理员已发送的消息
    /// </summary>
    /// <param name="request">动态查询请求</param>
    /// <returns>消息分页列表</returns>
    [HttpPost("list")]
    [ApiDescription("Query sent messages")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SystemMessageListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.SystemMessages.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _listAliases);
        var total = await query.CountAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var items = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(SystemMessage.CreatedAt), true, _listAliases)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .Select(x => new SystemMessageListItem(
                    x.Id, ServerTime.ToOffset(x.CreatedAt), x.UpdatedAt.HasValue ? ServerTime.ToOffset(x.UpdatedAt.Value) : null, x.Title, x.Content
                    , x.IsPopup, x.Recipients.Count
                )
            )
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<SystemMessageListItem>>.Ok(
                new PagedResponse<SystemMessageListItem>(items, request.Current, request.Size, total)
            )
        );
    }

    /// <summary>查询系统消息收件人状态明细</summary>
    /// <param name="id">消息主键</param>
    /// <returns>收件人状态明细</returns>
    [HttpGet("{id:long}/recipients")]
    [ApiDescription("Query system message recipients")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SystemMessageRecipientItem>>>> RecipientsAsync(long id) {
        var exists = await db.SystemMessages.AsNoTracking().AnyAsync(x => x.Id == id, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!exists) {
            return NotFound(new ApiResponse<IReadOnlyList<SystemMessageRecipientItem>>(404, "Message not found", null));
        }

        var items = await db
            .UserMessages.AsNoTracking()
            .Where(x => x.MessageId == id)
            .OrderBy(x => x.User.UserName)
            .Select(x => new SystemMessageRecipientItem(x.UserId, x.User.UserName, x.User.Email, x.IsRead, x.IsDeleted))
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<SystemMessageRecipientItem>>.Ok(items));
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

        var senderId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var message = new SystemMessage { SenderId = senderId, Title = request.Title.Trim(), Content = request.Content, IsPopup = request.IsPopup };
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
    [HttpPost("{id:long}")]
    [ApiDescription("Update system message")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAsync(
        long id
        , UpdateSystemMessageRequest request
    ) {
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

    private async Task<HashSet<long>> ResolveUserIdsAsync(
        string targetType
        , long[] departmentIds
        , long[] userIds
    ) {
        switch (targetType) {
            case "all":
                return await db
                    .Users.AsNoTracking()
                    .Where(x => x.IsEnabled)
                    .Select(x => x.Id)
                    .ToHashSetAsync(HttpContext.RequestAborted)
                    .ConfigureAwait(false);
            case "user":
                return await db
                    .Users.AsNoTracking()
                    .Where(x => x.IsEnabled && userIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToHashSetAsync(HttpContext.RequestAborted)
                    .ConfigureAwait(false);
        }

        var selected = departmentIds.Distinct().ToHashSet();
        if (targetType == "department_children") {
            var departments = await db
                .Departments.AsNoTracking()
                .Select(x => new { x.Id, x.ParentId })
                .ToListAsync(HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var changed = true;
            while (changed) {
                changed = departments
                .Where(x => x.ParentId.HasValue && selected.Contains(x.ParentId.Value))
                .Aggregate(
                    false, (
                        current
                        , item
                    ) => current || selected.Add(item.Id)
                );
            }
        }

        return await db
            .UserDepartments.AsNoTracking()
            .Where(x => selected.Contains(x.DepartmentId) && x.User.IsEnabled)
            .Select(x => x.UserId)
            .ToHashSetAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
    }
}