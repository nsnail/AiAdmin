namespace AiAdmin.Api.Contracts;

/// <summary>
///     校正用户在线状态的结果
/// </summary>
/// <param name="OfflineCount">本次因没有有效连接而置为离线的用户数量</param>
public sealed record RefreshUserOnlineStatusResult(int OfflineCount);