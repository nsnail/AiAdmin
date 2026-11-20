using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证用户在线状态：连接建立时用户置为在线，同一用户多个连接时最后一个断开才置为离线并记录最后在线时间，
///     过期的连接不算在线，状态没有变化时不写库，以及校正作业只把真正没有有效连接的用户置为离线
/// </summary>
public sealed class UserPresenceTests : IDisposable
{
    private const long _ALICE = 11;
    private const long _BOB = 12;
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserPresenceTests" /> class.
    ///     初始化内存数据库并写入两个测试用户
    /// </summary>
    public UserPresenceTests() {
        var connectionString = $"DataSource=file:presence-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        using var db = NewDb();
        _ = db.Database.EnsureCreated();
        _ = db.Users.Add(new User { Id = _ALICE, UserName = "alice", PasswordHash = "x" });
        _ = db.Users.Add(new User { Id = _BOB, UserName = "bob", PasswordHash = "x" });
        _ = db.SaveChanges();
    }

    /// <summary>
    ///     连接建立时用户置为在线，从未在线过的用户最后在线时间仍为空
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ConnectingMarksUserOnlineAsync() {
        var service = NewService(new FakePresenceStore());

        await service.ConnectedAsync(_ALICE, Guid.NewGuid()).ConfigureAwait(true);

        var (isOnline, lastOnlineAt) = await ReadAsync(_ALICE).ConfigureAwait(true);
        Assert.True(isOnline);
        Assert.Null(lastOnlineAt);
        Assert.False((await ReadAsync(_BOB).ConfigureAwait(true)).IsOnline);
    }

    /// <summary>
    ///     释放数据库连接
    /// </summary>
    public void Dispose() {
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     校正作业只把库里标记为在线、但没有有效连接的用户置为离线，仍有有效连接的用户保持在线
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ReconcileOnlyFlipsUsersWithoutValidConnectionAsync() {
        var store = new FakePresenceStore();
        var service = NewService(store);
        await service.ConnectedAsync(_ALICE, Guid.NewGuid()).ConfigureAwait(true);
        await service.ConnectedAsync(_BOB, Guid.NewGuid()).ConfigureAwait(true);

        // 模拟实例崩溃：Alice 的连接登记过期，库里仍标记为在线
        store.ExpireUser(_ALICE);
        var count = await service.ReconcileAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(1, count);
        Assert.False((await ReadAsync(_ALICE).ConfigureAwait(true)).IsOnline);
        _ = Assert.NotNull((await ReadAsync(_ALICE).ConfigureAwait(true)).LastOnlineAt);
        Assert.True((await ReadAsync(_BOB).ConfigureAwait(true)).IsOnline);
    }

    /// <summary>
    ///     没有任何在线用户时校正作业直接返回，不访问 Redis
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task ReconcileWithNobodyOnlineReturnsZeroAsync() {
        var service = NewService(new FakePresenceStore());

        Assert.Equal(0, await service.ReconcileAsync(CancellationToken.None).ConfigureAwait(true));
    }

    /// <summary>
    ///     状态没有变化时不写库：用户已经在线时再来一个连接不改变任何数据，已经离线时再注销也不会刷新最后在线时间
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task UnchangedStateDoesNotWriteAsync() {
        var service = NewService(new FakePresenceStore());
        var first = Guid.NewGuid();
        await service.ConnectedAsync(_ALICE, first).ConfigureAwait(true);
        await service.DisconnectedAsync(_ALICE, first).ConfigureAwait(true);
        var firstOffline = (await ReadAsync(_ALICE).ConfigureAwait(true)).LastOnlineAt;

        // 已经离线的用户再次注销，最后在线时间保持第一次的值
        await Task.Delay(30).ConfigureAwait(true);
        await service.DisconnectedAsync(_ALICE, Guid.NewGuid()).ConfigureAwait(true);

        Assert.Equal(firstOffline, (await ReadAsync(_ALICE).ConfigureAwait(true)).LastOnlineAt);
    }

    /// <summary>
    ///     同一用户开多个连接时，关掉其中一个仍然在线，最后一个断开才置为离线并记录最后在线时间
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task UserGoesOfflineOnlyWhenLastConnectionClosesAsync() {
        var service = NewService(new FakePresenceStore());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await service.ConnectedAsync(_ALICE, first).ConfigureAwait(true);
        await service.ConnectedAsync(_ALICE, second).ConfigureAwait(true);

        await service.DisconnectedAsync(_ALICE, first).ConfigureAwait(true);
        Assert.True((await ReadAsync(_ALICE).ConfigureAwait(true)).IsOnline);

        await service.DisconnectedAsync(_ALICE, second).ConfigureAwait(true);
        var (isOnline, lastOnlineAt) = await ReadAsync(_ALICE).ConfigureAwait(true);
        Assert.False(isOnline);
        Assert.True(DateTime.UtcNow - Assert.NotNull(lastOnlineAt) < TimeSpan.FromMinutes(1));
    }

    /// <summary>
    ///     创建主库上下文
    /// </summary>
    /// <returns>应用数据库上下文</returns>
    private AppDbContext NewDb() {
        var scope = new DataScopeContext();
        scope.Initialize(1, true, false, new HashSet<long>(), 100);
        return new AppDbContext(_options, scope, null!);
    }

    /// <summary>
    ///     创建在线状态服务，数据库使用内存库
    /// </summary>
    /// <param name="store">假 Redis 数据</param>
    /// <returns>在线状态服务</returns>
    private UserPresenceService NewService(FakePresenceStore store) {
        var services = new ServiceCollection();
        _ = services.AddScoped(_ => NewDb());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new UserPresenceService(store.CreateMultiplexer(), scopes, NullLogger<UserPresenceService>.Instance);
    }

    /// <summary>
    ///     读取用户当前的在线状态和最后在线时间
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <returns>是否在线和最后在线时间</returns>
    private async Task<(bool IsOnline, DateTime? LastOnlineAt)> ReadAsync(long userId) {
        await using var db = NewDb();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId).ConfigureAwait(false);
        return (user.IsOnline, user.LastOnlineAt);
    }

    /// <summary>
    ///     数据库代理，把调用转给假 Redis 数据
    /// </summary>
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class DatabaseProxy : DispatchProxy
    {
        /// <summary>
        ///     所属的假 Redis 数据
        /// </summary>
        public FakePresenceStore? Owner { get; set; }

        /// <summary>
        ///     拦截数据库调用
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        /// <exception cref="InvalidOperationException">代理没有绑定假 Redis 数据时引发</exception>
        protected override object Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return (Owner ?? throw new InvalidOperationException("Owner is not set")).Execute(targetMethod!.Name, args ?? []);
        }
    }

    /// <summary>
    ///     在线状态服务用到的有序集合操作的内存数据，并负责生成指向自己的连接复用器代理
    /// </summary>
    private sealed class FakePresenceStore
    {
        private readonly Dictionary<string, Dictionary<string, double>> _sortedSets = [];

        /// <summary>
        ///     创建连接复用器代理，数据库调用都转到本对象
        /// </summary>
        /// <returns>连接复用器代理</returns>
        public IConnectionMultiplexer CreateMultiplexer() {
            var database = DispatchProxy.Create<IDatabase, DatabaseProxy>();

            // ReSharper disable once SuspiciousTypeConversion.Global
            ((DatabaseProxy)database).Owner = this;
            var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, MultiplexerProxy>();

            // ReSharper disable once SuspiciousTypeConversion.Global
            ((MultiplexerProxy)multiplexer).Database = database;
            return multiplexer;
        }

        /// <summary>
        ///     让指定用户的连接登记过期，模拟实例崩溃后没有续期
        /// </summary>
        /// <param name="userId">用户主键</param>
        public void ExpireUser(long userId) {
            foreach (var members in _sortedSets.Where(x => x.Key.EndsWith($":conn:{userId}", StringComparison.Ordinal)).Select(x => x.Value)) {
                foreach (var member in members.Keys.ToList()) {
                    members[member] = 0;
                }
            }
        }

        /// <summary>
        ///     执行数据库方法调用，只实现被测服务用到的几个方法，其余视为测试遗漏直接报错
        /// </summary>
        /// <param name="method">被调用的方法名</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        /// <exception cref="NotSupportedException">调用了未实现的方法时引发</exception>
        internal object Execute(
            string method
            , object?[] args
        ) {
            var key = args.Length > 0 && args[0] is RedisKey redisKey ? (string?)redisKey ?? string.Empty : string.Empty;
            return method switch
            {
                "SortedSetAddAsync" => Task.FromResult(
                    AddSorted(key, args[1]?.ToString() ?? string.Empty, Convert.ToDouble(args[2], CultureInfo.InvariantCulture))
                )
                , "SortedSetRemoveAsync" => Task.FromResult(
                    _sortedSets.TryGetValue(key, out var members) && members.Remove(args[1]?.ToString() ?? string.Empty)
                )
                , "SortedSetRemoveRangeByScoreAsync" => Task.FromResult(RemoveByScore(key, Convert.ToDouble(args[2], CultureInfo.InvariantCulture)))
                , "SortedSetLengthAsync" => Task.FromResult(_sortedSets.TryGetValue(key, out var live) ? live.Count : 0L)
                , "KeyExpireAsync" => Task.FromResult(true)
                , _ => throw new NotSupportedException(method)
            };
        }

        /// <summary>
        ///     向有序集合加入或更新成员
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="member">成员</param>
        /// <param name="score">分值</param>
        /// <returns>新增成员返回 true</returns>
        private bool AddSorted(
            string key
            , string member
            , double score
        ) {
            if (!_sortedSets.TryGetValue(key, out var members)) {
                members = [];
                _sortedSets[key] = members;
            }

            var added = !members.ContainsKey(member);
            members[member] = score;
            return added;
        }

        /// <summary>
        ///     删除分值不大于上限的成员；服务用当前时间作上限，正常登记的分值在未来，过期的成员分值被设为 0，因此只有过期成员会被删除
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="max">分值上限</param>
        /// <returns>删除的成员数</returns>
        private long RemoveByScore(
            string key
            , double max
        ) {
            if (!_sortedSets.TryGetValue(key, out var members)) {
                return 0;
            }

            var expired = members.Where(x => x.Value <= max).Select(x => x.Key).ToList();
            foreach (var member in expired) {
                _ = members.Remove(member);
            }

            return expired.Count;
        }
    }

    /// <summary>
    ///     连接复用器代理，只提供数据库
    /// </summary>
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class MultiplexerProxy : DispatchProxy
    {
        /// <summary>
        ///     返回的数据库
        /// </summary>
        public IDatabase? Database { get; set; }

        /// <summary>
        ///     拦截连接复用器调用
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        /// <exception cref="NotSupportedException">调用了除 GetDatabase 以外的方法时引发</exception>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return targetMethod!.Name == nameof(IConnectionMultiplexer.GetDatabase) ? Database : throw new NotSupportedException(targetMethod.Name);
        }
    }
}