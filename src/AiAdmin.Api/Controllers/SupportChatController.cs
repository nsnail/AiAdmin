using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     在线客服控制器：用户通过顶部聊天浮层向客服发消息，客服在在线客服页面查看会话并回复，消息经 WebSocket 实时推送
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="realtime">实时连接管理器</param>
[ApiController]
[ApiDescription("Online customer service")]
[Route("api/support")]
public sealed class SupportChatController(AppDbContext db, RealtimeConnectionManager realtime) : ControllerBase
{
    private const int _PAGE_SIZE = 30;
    private const int _PREVIEW_LENGTH = 100;

    private static readonly IReadOnlyDictionary<string, string> _listAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = nameof(SupportConversation.Id)
        , ["createdAt"] = nameof(SupportConversation.CreatedAt)
        , ["isClosed"] = nameof(SupportConversation.IsClosed)
        , ["lastMessageAt"] = nameof(SupportConversation.LastMessageAt)
        , ["serviceUnreadCount"] = nameof(SupportConversation.ServiceUnreadCount)
        , ["userName"] = $"{nameof(SupportConversation.User)}.{nameof(Models.User.UserName)}"
    };

    /// <summary>
    ///     客服关闭会话，用户再次发言时会话自动重新打开
    /// </summary>
    /// <param name="request">会话标识请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("close")]
    [ApiDescription("Close support conversation")]
    public async Task<ActionResult<ApiResponse<object>>> CloseAsync([FromBody] IdentifierRequest request) {
        var conversation = await db.SupportConversations.SingleOrDefaultAsync(x => x.Id == request.Id, HttpContext.RequestAborted).ConfigureAwait(false);
        if (conversation is null) {
            return NotFound(new ApiResponse<object>(404, "Conversation not found", null));
        }

        conversation.IsClosed = true;
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Conversation closed"));
    }

    /// <summary>
    ///     查询客服会话列表筛选字段元数据
    /// </summary>
    /// <returns>筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query support conversation filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<SupportConversation>()));
    }

    /// <summary>
    ///     客服查询会话列表，默认按最后消息时间倒序
    /// </summary>
    /// <param name="request">动态查询请求</param>
    /// <returns>会话分页列表</returns>
    [HttpPost("list")]
    [ApiDescription("Query support conversations")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SupportConversationListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.SupportConversations.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter, _listAliases);
        var total = await query.CountAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var items = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(SupportConversation.LastMessageAt), true, _listAliases)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .Select(x => new SupportConversationListItem(
                    x.Id, ServerTime.ToOffset(x.CreatedAt), x.UserId, x.User.UserName, x.User.Avatar, x.LastMessagePreview
                    , ServerTime.ToOffset(x.LastMessageAt), x.ServiceUnreadCount, x.IsClosed
                )
            )
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<PagedResponse<SupportConversationListItem>>.Ok(new PagedResponse<SupportConversationListItem>(items, request.Current, request.Size, total)));
    }

    /// <summary>
    ///     客服查询指定会话的消息，并把用户消息标记为客服已读
    /// </summary>
    /// <param name="conversationId">会话主键</param>
    /// <param name="beforeId">只返回该消息主键之前的更早消息，首次加载不传</param>
    /// <returns>会话消息分页结果</returns>
    [HttpGet("messages")]
    [ApiDescription("Query support conversation messages")]
    public async Task<ActionResult<ApiResponse<SupportMessagePageResult>>> MessagesAsync(
        [FromQuery] long conversationId
        , [FromQuery] long? beforeId = null
    ) {
        var conversation = await db.SupportConversations.SingleOrDefaultAsync(x => x.Id == conversationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (conversation is null) {
            return NotFound(new ApiResponse<SupportMessagePageResult>(404, "Conversation not found", null));
        }

        var page = await LoadMessagesAsync(conversationId, beforeId).ConfigureAwait(false);
        if (beforeId is null && conversation.ServiceUnreadCount > 0) {
            conversation.ServiceUnreadCount = 0;
            _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            await realtime.PushSupportReadAsync(conversation.UserId, conversation.Id, conversation.UserUnreadCount, 0).ConfigureAwait(false);
        }

        return Ok(ApiResponse<SupportMessagePageResult>.Ok(page));
    }

    /// <summary>
    ///     用户查询自己的客服会话消息，并把客服消息标记为用户已读
    /// </summary>
    /// <param name="beforeId">只返回该消息主键之前的更早消息，首次加载不传</param>
    /// <returns>会话消息分页结果，用户还没有会话时返回空列表</returns>
    [HttpGet("my/messages")]
    [ApiDescription("Query my support messages")]
    public async Task<ActionResult<ApiResponse<SupportMessagePageResult>>> MyMessagesAsync([FromQuery] long? beforeId = null) {
        var userId = GetUserId();
        var conversation = await db.SupportConversations.SingleOrDefaultAsync(x => x.UserId == userId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (conversation is null) {
            return Ok(ApiResponse<SupportMessagePageResult>.Ok(new SupportMessagePageResult([], false)));
        }

        var page = await LoadMessagesAsync(conversation.Id, beforeId).ConfigureAwait(false);
        if (beforeId is null && conversation.UserUnreadCount > 0) {
            conversation.UserUnreadCount = 0;
            _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            await realtime.PushSupportReadAsync(userId, conversation.Id, 0, conversation.ServiceUnreadCount).ConfigureAwait(false);
        }

        return Ok(ApiResponse<SupportMessagePageResult>.Ok(page));
    }

    /// <summary>
    ///     查询当前用户自己的客服会话状态，用于聊天按钮角标
    /// </summary>
    /// <returns>会话标识和用户未读数量</returns>
    [HttpGet("my/state")]
    [ApiDescription("Query my support conversation state")]
    public async Task<ActionResult<ApiResponse<SupportConversationStateResult>>> MyStateAsync() {
        var userId = GetUserId();
        var state = await db
            .SupportConversations.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new SupportConversationStateResult(x.Id, x.UserUnreadCount))
            .SingleOrDefaultAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(ApiResponse<SupportConversationStateResult>.Ok(state ?? new SupportConversationStateResult(null, 0)));
    }

    /// <summary>
    ///     客服回复用户，必须指定会话
    /// </summary>
    /// <param name="request">会话标识和消息正文</param>
    /// <returns>已发送的消息</returns>
    [HttpPost("reply")]
    [ApiDescription("Reply support conversation")]
    public async Task<ActionResult<ApiResponse<SupportMessageItem>>> ReplyAsync([FromBody] SupportSendRequest request) {
        var content = request.Content.Trim();
        if (request.ConversationId is null || content.Length == 0) {
            return BadRequest(new ApiResponse<SupportMessageItem>(400, "Conversation and content are required", null));
        }

        var conversation = await db
            .SupportConversations.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.ConversationId.Value, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (conversation is null) {
            return NotFound(new ApiResponse<SupportMessageItem>(404, "Conversation not found", null));
        }

        var senderId = GetUserId();
        var message = new SupportMessage { ConversationId = conversation.Id, Content = content, IsFromService = true, SenderId = senderId };
        _ = db.SupportMessages.Add(message);
        conversation.LastMessagePreview = BuildPreview(content);
        conversation.LastMessageAt = message.CreatedAt;
        conversation.IsClosed = false;
        conversation.ServiceUnreadCount = 0;
        ++conversation.UserUnreadCount;
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);

        var senderName = await db.Users.Where(x => x.Id == senderId).Select(x => x.UserName).SingleAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var item = ToItem(message, senderName);
        await realtime.PushSupportMessageAsync(conversation.UserId, new RealtimeSupportMessageResult(item, conversation.UserId, conversation.User.UserName, 0, conversation.UserUnreadCount)).ConfigureAwait(false);
        return Ok(ApiResponse<SupportMessageItem>.Ok(item));
    }

    /// <summary>
    ///     用户向客服发消息，首次发言时自动创建会话，已关闭的会话自动重新打开
    /// </summary>
    /// <param name="request">消息正文</param>
    /// <returns>已发送的消息</returns>
    [HttpPost("send")]
    [ApiDescription("Send message to customer service")]
    public async Task<ActionResult<ApiResponse<SupportMessageItem>>> SendAsync([FromBody] SupportSendRequest request) {
        var content = request.Content.Trim();
        if (content.Length == 0) {
            return BadRequest(new ApiResponse<SupportMessageItem>(400, "Content is required", null));
        }

        var userId = GetUserId();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, HttpContext.RequestAborted).ConfigureAwait(false);
        var conversation = await db.SupportConversations.SingleOrDefaultAsync(x => x.UserId == userId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (conversation is null) {
            conversation = new SupportConversation { UserId = userId };
            _ = db.SupportConversations.Add(conversation);
        }

        var message = new SupportMessage { ConversationId = conversation.Id, Content = content, IsFromService = false, SenderId = userId };
        _ = db.SupportMessages.Add(message);
        conversation.LastMessagePreview = BuildPreview(content);
        conversation.LastMessageAt = message.CreatedAt;
        conversation.IsClosed = false;
        conversation.UserUnreadCount = 0;
        ++conversation.ServiceUnreadCount;
        _ = await db.SaveChangesAsync(HttpContext.RequestAborted).ConfigureAwait(false);

        var item = ToItem(message, user.UserName);
        await realtime.PushSupportMessageAsync(userId, new RealtimeSupportMessageResult(item, userId, user.UserName, conversation.ServiceUnreadCount, 0)).ConfigureAwait(false);
        return Ok(ApiResponse<SupportMessageItem>.Ok(item));
    }

    /// <summary>
    ///     生成会话列表中展示的消息摘要
    /// </summary>
    /// <param name="content">消息正文</param>
    /// <returns>截断后的摘要</returns>
    private static string BuildPreview(string content) {
        return content.Length <= _PREVIEW_LENGTH ? content : content[.._PREVIEW_LENGTH];
    }

    /// <summary>
    ///     把消息实体转换为返回项
    /// </summary>
    /// <param name="message">消息实体</param>
    /// <param name="senderName">发送人用户名</param>
    /// <returns>消息返回项</returns>
    private static SupportMessageItem ToItem(
        SupportMessage message
        , string senderName
    ) {
        return new SupportMessageItem(
            message.Id, message.ConversationId, message.Content, message.IsFromService, message.SenderId, senderName, ServerTime.ToOffset(message.CreatedAt)
        );
    }

    /// <summary>
    ///     获取当前登录用户主键
    /// </summary>
    /// <returns>当前用户主键</returns>
    private long GetUserId() {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     按时间倒序取一页消息再反转为正序，多取一条用于判断是否还有更早消息
    /// </summary>
    /// <param name="conversationId">会话主键</param>
    /// <param name="beforeId">只取该消息主键之前的消息</param>
    /// <returns>消息分页结果</returns>
    private async Task<SupportMessagePageResult> LoadMessagesAsync(
        long conversationId
        , long? beforeId
    ) {
        var query = db.SupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (beforeId is not null) {
            query = query.Where(x => x.Id < beforeId.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(_PAGE_SIZE + 1)
            .Select(x => new { Message = x, SenderName = db.Users.Where(user => user.Id == x.SenderId).Select(user => user.UserName).FirstOrDefault() ?? string.Empty })
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        var hasMore = rows.Count > _PAGE_SIZE;
        var items = rows.Take(_PAGE_SIZE).Reverse().Select(x => ToItem(x.Message, x.SenderName)).ToList();
        return new SupportMessagePageResult(items, hasMore);
    }
}