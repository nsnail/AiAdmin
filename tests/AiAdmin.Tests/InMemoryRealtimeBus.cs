using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;

namespace AiAdmin.Tests;

/// <summary>
///     测试用内存推送总线：同进程内把发布的消息同步分发给所有订阅者，行为等同于单实例的 Redis 总线；可通过 <see cref="Peer" /> 把两个实例互联以模拟多实例
/// </summary>
public sealed class InMemoryRealtimeBus : IRealtimeBus
{
    private readonly List<Func<RealtimeBusMessage, Task>> _handlers = [];

    /// <summary>
    ///     对端总线，发布的消息会同时分发给对端的订阅者，用来模拟另一个 API 实例
    /// </summary>
    public InMemoryRealtimeBus? Peer { get; set; }

    /// <summary>
    ///     分发给本总线的订阅者，再分发给对端
    /// </summary>
    /// <param name="message">总线消息</param>
    /// <returns>分发完成的任务</returns>
    public async Task PublishAsync(RealtimeBusMessage message) {
        await DispatchLocalAsync(message).ConfigureAwait(false);
        if (Peer is not null) {
            await Peer.DispatchLocalAsync(message).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     注册订阅者
    /// </summary>
    /// <param name="handler">消息处理委托</param>
    public void Subscribe(Func<RealtimeBusMessage, Task> handler) {
        _handlers.Add(handler);
    }

    /// <summary>
    ///     只分发给本总线的订阅者
    /// </summary>
    /// <param name="message">总线消息</param>
    /// <returns>分发完成的任务</returns>
    private async Task DispatchLocalAsync(RealtimeBusMessage message) {
        foreach (var handler in _handlers.ToList()) {
            await handler(message).ConfigureAwait(false);
        }
    }
}