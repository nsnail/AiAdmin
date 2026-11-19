namespace AiAdmin.Api.Services;

/// <summary>
///     告警监控一次判定的结果
/// </summary>
/// <param name="Breached">是否命中报警条件，取不到有效值也视为命中</param>
/// <param name="Value">读取到的值，取不到值时为空字符串</param>
/// <param name="Message">状态说明，命中条件、正常或取不到值的原因</param>
public sealed record AlertMonitorOutcome(bool Breached, string Value, string Message);