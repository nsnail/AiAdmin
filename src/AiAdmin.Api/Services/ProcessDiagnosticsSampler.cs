using System.Diagnostics;
using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Services;

/// <summary>
///     进程诊断采样器：记录起点后在终点计算区间内的 CPU 占用，并读取线程池状态，仅用于诊断展示，不参与空闲判定
/// </summary>
public sealed class ProcessDiagnosticsSampler
{
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private readonly TimeSpan _startCpu = Process.GetCurrentProcess().TotalProcessorTime;

    /// <summary>
    ///     读取从创建采样器到现在的诊断信息
    /// </summary>
    /// <returns>进程诊断信息</returns>
    public ProcessDiagnostics Sample() {
        var elapsed = Stopwatch.GetElapsedTime(_startTicks);
        var used = Process.GetCurrentProcess().TotalProcessorTime - _startCpu;
        var cpu = elapsed.TotalMilliseconds > 0 ? used.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100 : 0;
        ThreadPool.GetMaxThreads(out var maxWorker, out _);
        ThreadPool.GetAvailableThreads(out var availableWorker, out _);
        return new ProcessDiagnostics(Math.Round(Math.Clamp(cpu, 0, 100), 1), maxWorker - availableWorker, ThreadPool.PendingWorkItemCount, System.Threading.Timer.ActiveCount);
    }
}