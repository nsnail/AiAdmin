using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证多实例部署下的实时推送：实例 A 发起的推送能到达连接在实例 B 上的用户，且每条连接只收到一次
/// </summary>
public sealed class CrossInstanceRealtimeTests : IDisposable
{
    private const long _SERVICE_ID = 1;
    private const long _USER_ID = 2;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _providerA;
    private readonly ServiceProvider _providerB;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CrossInstanceRealtimeTests" /> class.
    ///     初始化共享数据库，并创建两个互联总线的实例
    /// </summary>
    public CrossInstanceRealtimeTests() {
        var connectionString = $"DataSource=file:cross-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using (var init = NewDb(options)) {
            _ = init.Database.EnsureCreated();
            _ = init.Users.Add(new User { Id = _SERVICE_ID, UserName = "service", PasswordHash = "x" });
            _ = init.Users.Add(new User { Id = _USER_ID, UserName = "alice", PasswordHash = "x" });
            _ = init.SaveChanges();
        }

        _providerA = BuildInstance(options);
        _providerB = BuildInstance(options);

        // 互为对端：任意一端发布，两端的订阅者都会收到，等价于 Redis 频道广播
        ((InMemoryRealtimeBus)_providerA.GetRequiredService<IRealtimeBus>()).Peer
            = (InMemoryRealtimeBus)_providerB.GetRequiredService<IRealtimeBus>();
        _providerA.GetRequiredService<RealtimeConnectionManager>().Start();
        _providerB.GetRequiredService<RealtimeConnectionManager>().Start();
    }

    /// <summary>
    ///     释放服务容器和数据库连接
    /// </summary>
    public void Dispose() {
        _providerA.Dispose();
        _providerB.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    ///     验证客服在实例 A 回复，连接在实例 B 的用户和连接在实例 A 的客服都只收到一次
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task SupportMessage_ReachesUserConnectedToAnotherInstanceAsync() {
        var managerA = _providerA.GetRequiredService<RealtimeConnectionManager>();
        var managerB = _providerB.GetRequiredService<RealtimeConnectionManager>();
        using var serviceSocket = new FakeSocket();
        using var userSocket = new FakeSocket();
        var service = ConnectAsync(managerA, serviceSocket, _SERVICE_ID, true);
        var user = ConnectAsync(managerB, userSocket, _USER_ID, false);
        await Task.WhenAll(serviceSocket.WaitForCountAsync(2), userSocket.WaitForCountAsync(2)).ConfigureAwait(true);

        var item = new SupportMessageItem(10, 20, "hello", true, _SERVICE_ID, "service", DateTimeOffset.UtcNow);
        await managerA.PushSupportMessageAsync(_USER_ID, new RealtimeSupportMessageResult(item, _USER_ID, "alice", 0, 1)).ConfigureAwait(true);

        await Task.WhenAll(serviceSocket.WaitForCountAsync(3), userSocket.WaitForCountAsync(3)).ConfigureAwait(true);
        await Task.Delay(200).ConfigureAwait(true);
        Assert.Equal(3, serviceSocket.Sent.Count);
        Assert.Equal(3, userSocket.Sent.Count);
        Assert.Equal("support-message", userSocket.Sent[2].GetProperty("type").GetString());

        serviceSocket.Close();
        userSocket.Close();
        await Task.WhenAll(service, user).ConfigureAwait(true);
    }

    /// <summary>
    ///     验证站内信未读数推送同样能跨实例到达，且数据来自共享数据库
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task Unread_ReachesUserConnectedToAnotherInstanceAsync() {
        var managerA = _providerA.GetRequiredService<RealtimeConnectionManager>();
        var managerB = _providerB.GetRequiredService<RealtimeConnectionManager>();
        using var userSocket = new FakeSocket();
        var user = ConnectAsync(managerB, userSocket, _USER_ID, false);
        await userSocket.WaitForCountAsync(2).ConfigureAwait(true);

        await managerA.NotifyUnreadAsync([_USER_ID], true).ConfigureAwait(true);

        await userSocket.WaitForCountAsync(3).ConfigureAwait(true);
        await Task.Delay(200).ConfigureAwait(true);
        Assert.Equal(3, userSocket.Sent.Count);
        Assert.Equal("unread", userSocket.Sent[2].GetProperty("type").GetString());
        Assert.True(userSocket.Sent[2].GetProperty("data").GetProperty("hasNew").GetBoolean());

        userSocket.Close();
        await user.ConfigureAwait(true);
    }

    /// <summary>
    ///     创建一个模拟的 API 实例：独立的总线、连接管理器，共享同一个数据库
    /// </summary>
    /// <param name="options">数据库配置</param>
    /// <returns>实例的服务容器</returns>
    private static ServiceProvider BuildInstance(DbContextOptions<AppDbContext> options) {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped(_ => NewDb(options));
        _ = services.AddSingleton<SystemInfoService>();
        _ = services.AddSingleton<IRealtimeBus, InMemoryRealtimeBus>();
        _ = services.AddSingleton<RealtimeConnectionManager>();
        return services.BuildServiceProvider();
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
    ///     创建数据库上下文
    /// </summary>
    /// <param name="options">数据库配置</param>
    /// <returns>数据库上下文</returns>
    private static AppDbContext NewDb(DbContextOptions<AppDbContext> options) {
        return new AppDbContext(options, new DataScopeContext(), null!);
    }
}