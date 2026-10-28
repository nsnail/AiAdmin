using AiAdmin.Api.Caching;

namespace AiAdmin.Api.Logging;

/// <summary>
///     API 请求日志入库配置，请求元数据先进入 Redis 队列，再由后台服务批量写入按天分表
/// </summary>
public sealed class ApiLogStorageOptions
{
    /// <summary>
    ///     单次批量写入的最大日志数量
    /// </summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>
    ///     是否启用请求日志入库
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     队列为空时的轮询间隔
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///     Redis 队列最大长度，超出后丢弃最旧的日志，避免数据库不可用时队列无限增长
    /// </summary>
    public int QueueCapacity { get; init; } = 100_000;

    /// <summary>
    ///     Redis 队列键
    /// </summary>
    public string QueueKey { get; init; } = RedisKeyPrefix.VALUE + "logs:api:database";
}