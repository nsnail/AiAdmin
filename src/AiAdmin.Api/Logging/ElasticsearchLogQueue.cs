using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace AiAdmin.Api.Logging;

/// <summary>
///     保存待写入 Elasticsearch 的日志内存队列
/// </summary>
[SuppressMessage(
    "Design", "CA1711:Identifiers should not have incorrect suffix"
    , Justification = "Queue accurately describes the channel-backed logging component."
)]
public sealed class ElasticsearchLogQueue
{
    private readonly Channel<ElasticsearchLogEntry> _channel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ElasticsearchLogQueue" /> class.
    ///     初始化日志队列
    /// </summary>
    /// <param name="options">日志输出配置</param>
    public ElasticsearchLogQueue(ElasticsearchLogOptions options)
    {
        var capacity = Math.Max(100, options.QueueCapacity);
        _channel = Channel.CreateBounded<ElasticsearchLogEntry>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false }
        );
    }

    /// <summary>
    ///     等待并读取下一条日志
    /// </summary>
    public ChannelReader<ElasticsearchLogEntry> Reader => _channel.Reader;

    /// <summary>
    ///     关闭日志队列写入端
    /// </summary>
    public void Complete()
    {
        _ = _channel.Writer.TryComplete();
    }

    /// <summary>
    ///     将日志写入内存队列
    /// </summary>
    /// <param name="entry">日志内容</param>
    /// <returns>日志成功进入队列时返回 true</returns>
    public bool TryEnqueue(ElasticsearchLogEntry entry)
    {
        return _channel.Writer.TryWrite(entry);
    }
}