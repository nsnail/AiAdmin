namespace AiAdmin.Api.Contracts;

/// <summary>
///     创建文件目录请求
/// </summary>
public sealed class CreateFileDirectoryRequest
{
    /// <summary>
    ///     目录名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     当前目录相对路径
    /// </summary>
    public string Path { get; init; } = string.Empty;
}