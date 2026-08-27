namespace AiAdmin.Api.Services;

/// <summary>
///     MinIO 文件对象信息
/// </summary>
/// <param name="Name">对象名称</param>
/// <param name="Size">对象大小</param>
/// <param name="LastModified">最后修改时间</param>
/// <param name="IsDirectory">是否为目录</param>
public sealed record MinioObject(string Name, long Size, DateTime LastModified, bool IsDirectory = false);