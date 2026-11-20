namespace AiAdmin.Api.Services;

/// <summary>
///     实时连接的在线登记：连接建立、续期和断开时通知，实现决定如何保存在线状态；
///     连接管理器只依赖这个接口，不和 Redis 绑定
/// </summary>
public interface IUserPresence
{
    /// <summary>
    ///     登记一个新连接
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步登记任务</returns>
    Task ConnectedAsync(
        long userId
        , Guid connectionId
    );

    /// <summary>
    ///     注销一个连接，用户已没有其他连接时记录最后在线时间
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步注销任务</returns>
    Task DisconnectedAsync(
        long userId
        , Guid connectionId
    );

    /// <summary>
    ///     续期一个连接的登记
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>异步续期任务</returns>
    Task RenewAsync(
        long userId
        , Guid connectionId
    );
}