namespace AiAdmin.Api.Services;

/// <summary>
///     MinIO 文件对象信息
/// </summary>
public sealed record MinioObject(string Name, long Size, DateTime LastModified, bool IsDirectory = false);