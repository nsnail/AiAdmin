using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证计划作业手动触发：队列的登记、读取、移除，以及手动触发跳过 Cron 判断但仍不打断运行中的作业
/// </summary>
public sealed class ScheduledJobManualTriggerTests
{
    /// <summary>
    ///     手动触发不要求 Cron 到期，也不要求启用；运行中的作业不会被重复触发
    /// </summary>
    [Fact]
    public void ManualTriggerSkipsCronButNotRunningJob() {
        // 每年 1 月 1 日 00:00 才到期，当前时间远离该时刻
        var now = new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var job = NewJob();

        Assert.False(ShouldTrigger(job, now, false));
        Assert.True(ShouldTrigger(job, now, true));

        job.IsEnabled = false;
        Assert.False(ShouldTrigger(job, now, false));
        Assert.True(ShouldTrigger(job, now, true));

        job.Status = ScheduledJobStatus.Running;
        Assert.False(ShouldTrigger(job, now, true));
    }

    /// <summary>
    ///     登记后可读取，重复登记不产生重复项，移除后不再出现
    /// </summary>
    /// <returns>异步测试任务</returns>
    [Fact]
    public async Task QueueEnqueuesReadsAndRemovesAsync() {
        var queue = new ScheduledJobManualTriggerQueue(FakeSetMultiplexer.Create());

        await queue.EnqueueAsync(11).ConfigureAwait(true);
        await queue.EnqueueAsync(11).ConfigureAwait(true);
        await queue.EnqueueAsync(22).ConfigureAwait(true);
        var pending = await queue.GetPendingAsync().ConfigureAwait(true);
        Assert.Equal(new long[] { 11, 22 }, pending.Order().ToList());

        await queue.RemoveAsync(11).ConfigureAwait(true);
        pending = await queue.GetPendingAsync().ConfigureAwait(true);
        Assert.Equal(new long[] { 22 }, pending.ToList());
    }

    /// <summary>
    ///     创建一个 Cron 表达式远离当前时间的作业
    /// </summary>
    /// <returns>计划作业</returns>
    private static ScheduledJob NewJob() {
        return new ScheduledJob { Name = "manual", CronExpression = "0 0 1 1 *", RequestUrl = "http://localhost/" };
    }

    /// <summary>
    ///     通过反射调用调度器的私有判断方法
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="now">当前时间</param>
    /// <param name="manual">是否存在手动触发请求</param>
    /// <returns>应触发返回 true</returns>
    private static bool ShouldTrigger(
        ScheduledJob job
        , DateTime now
        , bool manual
    ) {
        var method = typeof(ScheduledJobHostedService).GetMethod("ShouldTrigger", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, [job, now, manual])!;
    }

    /// <summary>
    ///     只实现 Set 的添加、读取和移除的内存假 Redis 数据库
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeSetDatabase : DispatchProxy
    {
        private readonly HashSet<long> _members = [];

        /// <summary>
        ///     拦截数据库调用，实现单值 Set 操作，其余返回默认值
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return targetMethod!.Name switch
            {
                "SetAddAsync" => Task.FromResult(_members.Add((long)(RedisValue)args![1]!))
                , "SetRemoveAsync" => Task.FromResult(_members.Remove((long)(RedisValue)args![1]!))
                , "SetMembersAsync" => Task.FromResult(_members.Select(x => (RedisValue)x).ToArray())
                , _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }
    }

    /// <summary>
    ///     只返回内存假数据库的连接复用器代理
    /// </summary>
    // DispatchProxy 需要在运行时继承该类型，不能密封
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime")]
    private class FakeSetMultiplexer : DispatchProxy
    {
        /// <summary>
        ///     GetDatabase 返回的假数据库
        /// </summary>
        private IDatabase? Database { get; set; }

        /// <summary>
        ///     创建连接复用器代理
        /// </summary>
        /// <returns>连接复用器代理</returns>
        public static IConnectionMultiplexer Create() {
            var multiplexer = Create<IConnectionMultiplexer, FakeSetMultiplexer>();

            // ReSharper disable once SuspiciousTypeConversion.Global
            ((FakeSetMultiplexer)multiplexer).Database = Create<IDatabase, FakeSetDatabase>();
            return multiplexer;
        }

        /// <summary>
        ///     拦截连接复用器调用：GetDatabase 返回假数据库，其余返回默认值
        /// </summary>
        /// <param name="targetMethod">被调用的方法</param>
        /// <param name="args">调用参数</param>
        /// <returns>调用结果</returns>
        protected override object? Invoke(
            MethodInfo? targetMethod
            , object?[]? args
        ) {
            return targetMethod!.Name == nameof(IConnectionMultiplexer.GetDatabase)
                ? Database
                : targetMethod.ReturnType.IsValueType
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
        }
    }
}