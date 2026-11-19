namespace AiAdmin.Api.Services;

/// <summary>
///     一次告警监控扫描的统计结果
/// </summary>
/// <param name="Scanned">扫描的监控数量</param>
/// <param name="Raised">本次新触发的报警数量</param>
/// <param name="Resolved">本次解除的报警数量</param>
/// <param name="Active">扫描结束后仍处于报警中的监控数量</param>
public sealed record AlertMonitorScanResult(int Scanned, int Raised, int Resolved, int Active);