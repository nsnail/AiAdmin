using System.Globalization;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Data;
using AiAdmin.Api.Middleware;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证实时 WebSocket 连接：连接时推送系统版本和未读数，站内信变化时向在线用户推送，未登录连接被拒绝
/// </summary>
public sealed class RealtimeWebSocketTests : IAsyncLifetime, IDisposable
{
    private const long _USER_ID = 1001;
    private WebApplication _app = null!;
    private Uri _baseUri = null!;
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    /// <summary>
    ///     验证连接建立后先推送系统版本，再推送当前未读数
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Connect_PushesSystemInfoThenUnreadCountAsync() {
        await SeedUnreadAsync(2).ConfigureAwait(true);
        using var socket = await ConnectAsync().ConfigureAwait(true);

        var first = await ReceiveAsync(socket).ConfigureAwait(true);
        Assert.Equal("system-info", first.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("data").GetProperty("version").GetString()));
        var second = await ReceiveAsync(socket).ConfigureAwait(true);
        Assert.Equal("unread", second.GetProperty("type").GetString());
        Assert.Equal(2, second.GetProperty("data").GetProperty("count").GetInt32());
        Assert.False(second.GetProperty("data").GetProperty("hasNew").GetBoolean());
    }

    /// <summary>
    ///     验证未登录的连接请求被拒绝
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Connect_WithoutLoginIsRejectedAsync() {
        using var socket = new ClientWebSocket();

        _ = await Assert
            .ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(new Uri(_baseUri, "/api/ws"), CancellationToken.None))
            .ConfigureAwait(true);
    }

    /// <summary>
    ///     释放数据库连接，满足可释放字段规则，实际清理在 DisposeAsync 中完成
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
    }

    /// <summary>
    ///     停止服务并释放数据库连接
    /// </summary>
    /// <returns>异步清理任务</returns>
    public async Task DisposeAsync() {
        await _app.StopAsync().ConfigureAwait(true);
        await _app.DisposeAsync().ConfigureAwait(true);
        await _connection.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     创建内存数据库并启动只包含实时中间件的最小 Kestrel 服务，登录态由请求头模拟
    /// </summary>
    /// <returns>异步初始化任务</returns>
    public async Task InitializeAsync() {
        var connectionString = $"DataSource=file:realtime-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync().ConfigureAwait(true);
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using (var init = NewDb()) {
            _ = await init.Database.EnsureCreatedAsync().ConfigureAwait(true);
            _ = init.Users.Add(new User { Id = _USER_ID, UserName = "realtime", PasswordHash = "x" });
            _ = await init.SaveChangesAsync().ConfigureAwait(true);
        }

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
        _ = builder.Logging.ClearProviders();
        _ = builder.Services.AddScoped(_ => NewDb());
        _ = builder.Services.AddDistributedMemoryCache();
        _ = builder.Services.AddSingleton<ApiPermissionCache>();
        _ = builder.Services.AddSingleton<SystemInfoService>();
        _ = builder.Services.AddSingleton<IRealtimeBus, InMemoryRealtimeBus>();
        _ = builder.Services.AddSingleton<RealtimeConnectionManager>();
        _app = builder.Build();
        _ = _app.UseWebSockets();

        // 测试中用请求头 X-Test-User 模拟已登录用户，真实环境由 JWT 认证中间件提供
        _ = _app.Use(async (
                context
                , next
            ) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-User", out var user)) {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test"));
                }

                await next(context).ConfigureAwait(false);
            }
        );
        _ = _app.UseMiddleware<RealtimeWebSocketMiddleware>();
        await _app.StartAsync().ConfigureAwait(true);
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _baseUri = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal));
    }

    /// <summary>
    ///     验证向在线用户推送未读数时携带最新数量，且已读、删除的消息不计入
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task NotifyUnread_PushesLatestCountToOnlineUserAsync() {
        using var socket = await ConnectAsync().ConfigureAwait(true);
        _ = await ReceiveAsync(socket).ConfigureAwait(true);
        _ = await ReceiveAsync(socket).ConfigureAwait(true);

        await SeedUnreadAsync(3).ConfigureAwait(true);
        await using (var db = NewDb()) {
            var read = await db.UserMessages.FirstAsync().ConfigureAwait(true);
            read.IsRead = true;
            _ = await db.SaveChangesAsync().ConfigureAwait(true);
        }

        await _app.Services.GetRequiredService<RealtimeConnectionManager>().NotifyUnreadAsync([_USER_ID], true).ConfigureAwait(true);

        var pushed = await ReceiveAsync(socket).ConfigureAwait(true);
        Assert.Equal("unread", pushed.GetProperty("type").GetString());
        Assert.Equal(2, pushed.GetProperty("data").GetProperty("count").GetInt32());
        Assert.True(pushed.GetProperty("data").GetProperty("hasNew").GetBoolean());
    }

    /// <summary>
    ///     验证文本 ping 会收到 pong，保持心跳可用
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Ping_RepliesPongAsync() {
        using var socket = await ConnectAsync().ConfigureAwait(true);
        _ = await ReceiveAsync(socket).ConfigureAwait(true);
        _ = await ReceiveAsync(socket).ConfigureAwait(true);

        await socket.SendAsync("ping"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal("pong", await ReceiveTextAsync(socket).ConfigureAwait(true));
    }

    /// <summary>
    ///     接收一条 JSON 信封消息
    /// </summary>
    /// <param name="socket">客户端 WebSocket</param>
    /// <returns>解析后的 JSON 根元素</returns>
    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket socket) {
        return JsonElement.Parse(await ReceiveTextAsync(socket).ConfigureAwait(true));
    }

    /// <summary>
    ///     接收一条文本消息，超时视为失败
    /// </summary>
    /// <param name="socket">客户端 WebSocket</param>
    /// <returns>消息文本</returns>
    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).ConfigureAwait(true);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    /// <summary>
    ///     以测试用户身份连接实时端点
    /// </summary>
    /// <returns>已连接的客户端 WebSocket</returns>
    private async Task<ClientWebSocket> ConnectAsync() {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-Test-User", _USER_ID.ToString(CultureInfo.InvariantCulture));
        await socket.ConnectAsync(new Uri(_baseUri, "/api/ws"), CancellationToken.None).ConfigureAwait(true);
        return socket;
    }

    /// <summary>
    ///     创建数据库上下文
    /// </summary>
    /// <returns>未启用数据权限过滤的数据库上下文</returns>
    private AppDbContext NewDb() {
        return new AppDbContext(_options, new DataScopeContext(), null!);
    }

    /// <summary>
    ///     为测试用户写入指定数量的未读站内信
    /// </summary>
    /// <param name="count">未读消息数量</param>
    /// <returns>异步写入任务</returns>
    private async Task SeedUnreadAsync(int count) {
        await using var db = NewDb();
        for (var i = 0; i < count; ++i) {
            var message = new SystemMessage { SenderId = _USER_ID, Title = $"t{i}", Content = "c" };
            message.Recipients.Add(new UserMessage { UserId = _USER_ID, Message = message });
            _ = db.SystemMessages.Add(message);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(true);
    }
}