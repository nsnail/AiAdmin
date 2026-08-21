namespace AiAdmin.Api.Logging;

/// <summary>
///     Elasticsearch 日志输出配置
/// </summary>
public sealed class ElasticsearchLogOptions
{
    /// <summary>
    ///     单次写入 Elasticsearch 的最大日志数量
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    ///     是否启用 Elasticsearch 日志输出
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     后台任务消费间隔
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     日志索引名称
    /// </summary>
    public string Index { get; set; } = "aiadmin-logs";

    /// <summary>
    ///     Elasticsearch 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     内存队列容量
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>
    ///     Redis 日志队列键名
    /// </summary>
    public string QueueKey { get; set; } = "aiadmin:logs:elasticsearch";

    /// <summary>
    ///     Elasticsearch 服务地址
    /// </summary>
    public string Uri { get; set; } = "http://localhost:9200";

    /// <summary>
    ///     Elasticsearch 用户名
    /// </summary>
    public string? Username { get; set; }
}