namespace AiAdmin.Api.Contracts;

/// <summary>
///     刷新仪表盘总览缓存的结果
/// </summary>
/// <param name="Refreshed">本次写入的缓存条数</param>
public sealed record RefreshDashboardOverviewResult(int Refreshed);