namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 服务器运行信息
/// </summary>
public sealed record RedisServerInfoResult(
    string Endpoint
    , string Version
    , string Mode
    , long ConnectedClients
    , string UsedMemory
    , string MaxMemory
    , long DatabaseSize
    , double CpuUsagePercent
    , long UptimeSeconds
    , double CacheHitRatePercent);