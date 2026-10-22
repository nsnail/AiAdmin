namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘按日趋势数据点
/// </summary>
/// <param name="Date">日期，格式 yyyy-MM-dd</param>
/// <param name="Primary">主要指标数量，如登录次数或成功次数</param>
/// <param name="Secondary">次要指标数量，如登录用户数或失败次数</param>
public sealed record DashboardTrendPoint(string Date, int Primary, int Secondary);