using AiAdmin.Api.Services;

namespace AiAdmin.Tests;

/// <summary>
///     什么都不做的在线登记，供不关心在线状态的实时连接测试使用，避免它们依赖 Redis
/// </summary>
public sealed class NoPresence : IUserPresence
{
    /// <summary>
    ///     不登记连接
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>已完成的任务</returns>
    public Task ConnectedAsync(
        long userId
        , Guid connectionId
    ) {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     不注销连接
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>已完成的任务</returns>
    public Task DisconnectedAsync(
        long userId
        , Guid connectionId
    ) {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     不续期连接
    /// </summary>
    /// <param name="userId">用户主键</param>
    /// <param name="connectionId">连接编号</param>
    /// <returns>已完成的任务</returns>
    public Task RenewAsync(
        long userId
        , Guid connectionId
    ) {
        return Task.CompletedTask;
    }
}