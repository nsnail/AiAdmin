namespace AiAdmin.Api.Logging;

/// <summary>
///     将 Microsoft 日志转换并写入 Elasticsearch 内存队列的日志提供程序
/// </summary>
/// <param name="queue">日志内存队列</param>
/// <param name="options">日志输出配置</param>
public sealed class ElasticsearchLoggerProvider(ElasticsearchLogQueue queue, ElasticsearchLogOptions options) : ILoggerProvider
{
    /// <summary>
    ///     创建指定分类的日志记录器
    /// </summary>
    /// <param name="categoryName">日志分类名称</param>
    /// <returns>日志记录器</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new ElasticsearchLogger(categoryName, queue, options);
    }

    /// <summary>
    ///     释放日志提供程序
    /// </summary>
    public void Dispose()
    {
        queue.Complete();
    }
}