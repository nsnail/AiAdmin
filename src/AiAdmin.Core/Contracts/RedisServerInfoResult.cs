namespace AiAdmin.Api.Contracts;

/// <summary>
///     Redis 服务器运行信息
/// </summary>
/// <param name="Endpoint">服务端点</param>
/// <param name="Version">版本</param>
/// <param name="Mode">运行模式</param>
/// <param name="ConnectedClients">客户端数量</param>
/// <param name="UsedMemory">已用内存</param>
/// <param name="MaxMemory">最大内存</param>
/// <param name="DatabaseSize">数据库大小</param>
/// <param name="CpuUsagePercent">CPU 使用率</param>
/// <param name="UptimeSeconds">运行秒数</param>
/// <param name="CacheHitRatePercent">缓存命中率</param>
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