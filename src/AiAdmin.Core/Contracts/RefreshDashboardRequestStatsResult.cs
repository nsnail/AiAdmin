namespace AiAdmin.Api.Contracts;

/// <summary>
///     刷新仪表盘操作日志统计缓存的结果
/// </summary>
/// <param name="Refreshed">本次写入的缓存条数</param>
public sealed record RefreshDashboardRequestStatsResult(int Refreshed);