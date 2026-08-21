namespace AiAdmin.Api.Contracts;

/// <summary>
///     删除文件或目录请求
/// </summary>
public sealed class DeleteFileRequest
{
    /// <summary>
    ///     是否递归删除目录
    /// </summary>
    public bool? Directory { get; init; }

    /// <summary>
    ///     对象相对路径
    /// </summary>
    public string Name { get; init; } = string.Empty;
}