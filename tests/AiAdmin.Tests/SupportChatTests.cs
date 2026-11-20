using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证在线客服的会话创建、消息收发、未读数、会话关闭，以及客服消息只推送给会话所属用户和客服
/// </summary>
public sealed class SupportChatTests : IDisposable
{
    private const long _OTHER_USER_ID = 3;
    private const long _SERVICE_ID = 1;
    private const long _USER_ID = 2;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ServiceProvider _provider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SupportChatTests" /> class.
    ///     初始化内存数据库、用户和服务容器
    /// </summary>
    public SupportChatTests() {
        var connectionString = $"DataSource=file:support-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using (var init = NewDb()) {
            _ = init.Database.EnsureCreated();
            _ = init.Users.Add(new User { Id = _SERVICE_ID, UserName = "service", PasswordHash = "x" });
            _ = init.Users.Add(new User { Id = _USER_ID, UserName = "alice", PasswordHash = "x" });
            _ = init.Users.Add(new User { Id = _OTHER_USER_ID, UserName = "bob", PasswordHash = "x" });
            _ = init.SaveChanges();
        }

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped(_ => NewDb());
        _ = services.AddSingleton<SystemInfoService>();
        _ = services.AddSingleton<IRealtimeBus, InMemoryRealtimeBus>();
        _ = services.AddSingleton<IUserPresence, NoPresence>();
        _ = services.AddSingleton<RealtimeConnectionManager>();
        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    ///     验证关闭的会话在用户再次发言后自动重新打开
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Close_ThenUserSendsAgainReopensConversationAsync() {
        var sent = await SendAsync(_USER_ID, "first").ConfigureAwait(true);
        _ = await NewController(_SERVICE_ID).CloseAsync(new IdentifierRequest { Id = sent.ConversationId }).ConfigureAwait(true);
        await using (var db = NewDb()) {
            Assert.True((await db.SupportConversations.SingleAsync().ConfigureAwait(true)).IsClosed);
        }

        _ = await SendAsync(_USER_ID, "again").ConfigureAwait(true);

        await using var check = NewDb();
        Assert.False((await check.SupportConversations.SingleAsync().ConfigureAwait(true)).IsClosed);
    }

    /// <summary>
    ///     释放服务容器和数据库连接
    /// </summary>
    public void Dispose() {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    ///     验证消息分页：首页返回最新一页并标记还有更早消息，按主键向前翻页取到最早的消息
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Messages_PagesBackwardsInChronologicalOrderAsync() {
        for (var i = 1; i <= 35; ++i) {
            _ = await SendAsync(_USER_ID, $"m{i}").ConfigureAwait(true);
        }

        var first = UnwrapPage(await NewController(_USER_ID).MyMessagesAsync().ConfigureAwait(true));
        Assert.Equal(30, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal("m6", first.Items[0].Content);
        Assert.Equal("m35", first.Items[^1].Content);

        var older = UnwrapPage(await NewController(_USER_ID).MyMessagesAsync(first.Items[0].Id).ConfigureAwait(true));
        Assert.Equal(["m1", "m2", "m3", "m4", "m5"], [.. older.Items.Select(x => x.Content)]);
        Assert.False(older.HasMore);
    }

    /// <summary>
    ///     验证实时推送范围：会话所属用户和客服连接都能收到，其他用户的连接收不到
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Push_ReachesOwnerAndServiceButNotOtherUsersAsync() {
        var manager = _provider.GetRequiredService<RealtimeConnectionManager>();
        using var ownerSocket = new FakeSocket();
        using var serviceSocket = new FakeSocket();
        using var otherSocket = new FakeSocket();
        var owner = ConnectAsync(manager, ownerSocket, _USER_ID, false);
        var service = ConnectAsync(manager, serviceSocket, _SERVICE_ID, true);
        var other = ConnectAsync(manager, otherSocket, _OTHER_USER_ID, false);
        await Task
            .WhenAll(ownerSocket.WaitForCountAsync(2), serviceSocket.WaitForCountAsync(2), otherSocket.WaitForCountAsync(2))
            .ConfigureAwait(true);

        var item = new SupportMessageItem(10, 20, "hello", false, _USER_ID, "alice", DateTimeOffset.UtcNow);
        await manager.PushSupportMessageAsync(_USER_ID, new RealtimeSupportMessageResult(item, _USER_ID, "alice", 1, 0)).ConfigureAwait(true);

        await Task.WhenAll(ownerSocket.WaitForCountAsync(3), serviceSocket.WaitForCountAsync(3)).ConfigureAwait(true);
        await Task.Delay(200).ConfigureAwait(true);
        Assert.Equal("support-message", ownerSocket.Sent[2].GetProperty("type").GetString());
        Assert.Equal("support-message", serviceSocket.Sent[2].GetProperty("type").GetString());
        Assert.Equal(2, otherSocket.Sent.Count);

        ownerSocket.Close();
        serviceSocket.Close();
        otherSocket.Close();
        await Task.WhenAll(owner, service, other).ConfigureAwait(true);
    }

    /// <summary>
    ///     验证客服回复累加用户未读数并清零客服未读数，客服查看消息后用户消息被标记已读
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Reply_CountsUserUnreadAndViewingClearsServiceUnreadAsync() {
        var sent = await SendAsync(_USER_ID, "need help").ConfigureAwait(true);

        var viewed = await NewController(_SERVICE_ID).MessagesAsync(sent.ConversationId).ConfigureAwait(true);
        _ = Assert.Single(UnwrapPage(viewed).Items);
        await using (var db = NewDb()) {
            Assert.Equal(0, (await db.SupportConversations.SingleAsync().ConfigureAwait(true)).ServiceUnreadCount);
        }

        var reply = await NewController(_SERVICE_ID)
            .ReplyAsync(new SupportSendRequest { ConversationId = sent.ConversationId, Content = "sure" })
            .ConfigureAwait(true);
        Assert.True(Unwrap(reply).IsFromService);
        await using var check = NewDb();
        var conversation = await check.SupportConversations.SingleAsync().ConfigureAwait(true);
        Assert.Equal(1, conversation.UserUnreadCount);
        Assert.Equal(0, conversation.ServiceUnreadCount);

        var mine = await NewController(_USER_ID).MyMessagesAsync().ConfigureAwait(true);
        Assert.Equal(["need help", "sure"], [.. UnwrapPage(mine).Items.Select(x => x.Content)]);
        await using var after = NewDb();
        Assert.Equal(0, (await after.SupportConversations.SingleAsync().ConfigureAwait(true)).UserUnreadCount);
    }

    /// <summary>
    ///     验证用户首次发言自动创建会话，客服未读数累加，用户未读数为零
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Send_CreatesConversationAndCountsServiceUnreadAsync() {
        var first = await SendAsync(_USER_ID, "hello").ConfigureAwait(true);
        _ = await SendAsync(_USER_ID, "anyone there").ConfigureAwait(true);

        await using var db = NewDb();
        var conversation = await db.SupportConversations.SingleAsync().ConfigureAwait(true);
        Assert.Equal(_USER_ID, conversation.UserId);
        Assert.Equal(first.ConversationId, conversation.Id);
        Assert.Equal(2, conversation.ServiceUnreadCount);
        Assert.Equal(0, conversation.UserUnreadCount);
        Assert.Equal("anyone there", conversation.LastMessagePreview);
        Assert.Equal(2, await db.SupportMessages.CountAsync().ConfigureAwait(true));
    }

    /// <summary>
    ///     验证空消息被拒绝，客服回复缺少会话时被拒绝
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Send_RejectsBlankContentAndReplyWithoutConversationAsync() {
        var blank = await NewController(_USER_ID).SendAsync(new SupportSendRequest { Content = "   " }).ConfigureAwait(true);
        _ = Assert.IsType<BadRequestObjectResult>(blank.Result);

        var noConversation = await NewController(_SERVICE_ID).ReplyAsync(new SupportSendRequest { Content = "hi" }).ConfigureAwait(true);
        _ = Assert.IsType<BadRequestObjectResult>(noConversation.Result);
    }

    /// <summary>
    ///     在后台任务中建立实时连接，连接结束时任务完成
    /// </summary>
    /// <param name="manager">实时连接管理器</param>
    /// <param name="socket">假套接字</param>
    /// <param name="userId">用户主键</param>
    /// <param name="isService">是否为客服连接</param>
    /// <returns>连接处理任务</returns>
    private static Task ConnectAsync(
        RealtimeConnectionManager manager
        , FakeSocket socket
        , long userId
        , bool isService
    ) {
        return Task.Run(() => manager.HandleAsync(socket, userId, isService, CancellationToken.None));
    }

    /// <summary>
    ///     取出控制器返回的数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="result">控制器返回结果</param>
    /// <returns>响应中的数据</returns>
    private static T Unwrap<T>(ActionResult<ApiResponse<T>> result) {
        return ((ApiResponse<T>)((OkObjectResult)result.Result!).Value!).Data!;
    }

    /// <summary>
    ///     取出消息分页结果
    /// </summary>
    /// <param name="result">控制器返回结果</param>
    /// <returns>消息分页数据</returns>
    private static SupportMessagePageResult UnwrapPage(ActionResult<ApiResponse<SupportMessagePageResult>> result) {
        return Unwrap(result);
    }

    /// <summary>
    ///     创建以指定用户身份调用的客服控制器
    /// </summary>
    /// <param name="userId">当前登录用户主键</param>
    /// <returns>在线客服控制器</returns>
    private SupportChatController NewController(long userId) {
        var controller = new SupportChatController(NewDb(), _provider.GetRequiredService<RealtimeConnectionManager>());
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture))], "test")
            )
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    /// <summary>
    ///     创建数据库上下文
    /// </summary>
    /// <returns>未启用数据权限过滤的数据库上下文</returns>
    private AppDbContext NewDb() {
        return new AppDbContext(_options, new DataScopeContext(), null!);
    }

    /// <summary>
    ///     以指定用户身份发送一条客服消息
    /// </summary>
    /// <param name="userId">发送用户主键</param>
    /// <param name="content">消息正文</param>
    /// <returns>已发送的消息</returns>
    private async Task<SupportMessageItem> SendAsync(
        long userId
        , string content
    ) {
        return Unwrap(await NewController(userId).SendAsync(new SupportSendRequest { Content = content }).ConfigureAwait(true));
    }
}