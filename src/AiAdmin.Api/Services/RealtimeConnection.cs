using System.Net.WebSockets;

namespace AiAdmin.Api.Services;

/// <summary>
///     单个已建立的实时 WebSocket 连接，WebSocket 不允许并发发送，因此携带发送锁
/// </summary>
/// <param name="userId">连接所属用户标识</param>
/// <param name="socket">底层 WebSocket</param>
/// <param name="isService">连接所属用户是否拥有客服工作台权限</param>
public sealed class RealtimeConnection(long userId, WebSocket socket, bool isService) : IDisposable
{
    /// <summary>
    ///     连接唯一标识
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    ///     连接所属用户是否拥有客服工作台权限，客服连接会收到所有用户的客服消息
    /// </summary>
    public bool IsService { get; } = isService;

    /// <summary>
    ///     发送锁，保证同一连接同一时刻只有一个发送操作
    /// </summary>
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    /// <summary>
    ///     底层 WebSocket
    /// </summary>
    public WebSocket Socket { get; } = socket;

    /// <summary>
    ///     连接所属用户标识
    /// </summary>
    public long UserId { get; } = userId;

    /// <summary>
    ///     释放发送锁
    /// </summary>
    public void Dispose() {
        SendLock.Dispose();
    }
}