using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     将错误及严重错误日志写入本地文件的日志提供程序
/// </summary>
/// <param name="options">文件日志配置选项</param>
public sealed class FileLoggerProvider(IOptions<FileLogOptions> options) : ILoggerProvider
{
    private readonly string _directory = ResolveDirectory(options.Value.Directory);
    private readonly FileLogOptions _options = options.Value;
    private readonly object _syncRoot = new();

    /// <summary>
    ///     创建指定分类的文件日志记录器
    /// </summary>
    /// <param name="categoryName">日志分类名称</param>
    /// <returns>文件日志记录器</returns>
    public ILogger CreateLogger(string categoryName) {
        return new FileLogger(categoryName, _options, _directory, _syncRoot);
    }

    /// <summary>
    ///     释放文件日志提供程序
    /// </summary>
    public void Dispose() {
    }

    /// <summary>
    ///     将配置目录解析为应用目录下的绝对路径
    /// </summary>
    /// <param name="directory">配置的日志目录</param>
    /// <returns>日志目录绝对路径</returns>
    private static string ResolveDirectory(string directory) {
        var configuredDirectory = string.IsNullOrWhiteSpace(directory) ? "logs" : directory.Trim();
        return Path.IsPathRooted(configuredDirectory) ? configuredDirectory : Path.Combine(AppContext.BaseDirectory, configuredDirectory);
    }
}