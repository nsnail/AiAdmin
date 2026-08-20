#pragma warning disable SA1518

namespace AiAdmin.Api.Contracts;

/// <summary>
///     创建文件目录请求
/// </summary>
public sealed class CreateFileDirectoryRequest
{
    /// <summary>当前目录相对路径</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>目录名称</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
///     删除文件或目录请求
/// </summary>
public sealed class DeleteFileRequest
{
    /// <summary>对象相对路径</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>是否递归删除目录</summary>
    public bool? Directory { get; init; }
}
