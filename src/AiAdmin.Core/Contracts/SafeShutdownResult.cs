namespace AiAdmin.Api.Contracts;

/// <summary>
///     安全停机结果，空闲时返回即表示调用方可以终止进程；超时时携带未归零的在途工作快照
/// </summary>
/// <param name="WaitedMilliseconds">本次调用等待的毫秒数</param>
/// <param name="InFlight">返回时各类别在途工作数量</param>
/// <param name="Diagnostics">等待期间的进程诊断信息，仅供排查，不参与空闲判定</param>
public sealed record SafeShutdownResult(long WaitedMilliseconds, IReadOnlyDictionary<string, int> InFlight, ProcessDiagnostics Diagnostics);