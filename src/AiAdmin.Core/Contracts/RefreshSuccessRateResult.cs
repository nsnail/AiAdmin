namespace AiAdmin.Api.Contracts;

/// <summary>
///     刷新作业成功率结果
/// </summary>
/// <param name="Total">参与统计的作业数量</param>
/// <param name="Changed">成功率发生变化的作业数量</param>
public sealed record RefreshSuccessRateResult(int Total, int Changed);