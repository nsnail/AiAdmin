using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     安全停机闸门是进程级静态状态，相关测试必须独占运行，避免被其他并行测试的日志干扰
/// </summary>
[CollectionDefinition("ShutdownGate", DisableParallelization = true)]
public sealed class ShutdownGateCollection;