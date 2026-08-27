namespace AiAdmin.Api.Logging;

/// <summary>
///     文件日志输出配置
/// </summary>
public sealed class FileLogOptions
{
    /// <summary>
    ///     是否启用文件日志输出
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     日志文件目录
    /// </summary>
    public string Directory { get; set; } = "logs";

    /// <summary>
    ///     日志文件名称前缀
    /// </summary>
    public string FileName { get; set; } = "error";
}