using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Services;

/// <summary>
///     实时推送总线：把推送意图广播给所有 API 实例，使多实例部署下任意实例发起的推送都能到达连接在其他实例上的用户
/// </summary>
public interface IRealtimeBus
{
    /// <summary>
    ///     发布一条消息，所有已订阅的实例（含自身）都会收到
    /// </summary>
    /// <param name="message">总线消息</param>
    /// <returns>发布完成的任务</returns>
    Task PublishAsync(RealtimeBusMessage message);

    /// <summary>
    ///     订阅总线消息，重复订阅会重复收到
    /// </summary>
    /// <param name="handler">消息处理委托</param>
    void Subscribe(Func<RealtimeBusMessage, Task> handler);
}