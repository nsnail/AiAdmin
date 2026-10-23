namespace AiAdmin.Api.Contracts;

/// <summary>
///     进程运行诊断信息，仅用于安全停机时辅助判断进程是在忙还是在空转，不参与空闲判定
/// </summary>
/// <param name="CpuPercent">采样区间内进程占用的 CPU 百分比，按全部逻辑核心折算，100 表示占满所有核心</param>
/// <param name="BusyThreads">线程池中正在执行工作的线程数</param>
/// <param name="QueuedWorkItems">线程池中排队等待执行的工作项数量</param>
/// <param name="PendingTimers">当前存活的计时器数量</param>
public sealed record ProcessDiagnostics(double CpuPercent, int BusyThreads, long QueuedWorkItems, long PendingTimers);