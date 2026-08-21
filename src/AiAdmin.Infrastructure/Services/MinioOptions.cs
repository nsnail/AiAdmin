namespace AiAdmin.Api.Services;

/// <summary>
///     MinIO 存储配置
/// </summary>
public sealed class MinioOptions
{
    /// <summary>
    ///     MinIO 访问密钥
    /// </summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>
    ///     MinIO 存储桶名称
    /// </summary>
    public string Bucket { get; init; } = "aiadmin";

    /// <summary>
    ///     MinIO 服务地址
    /// </summary>
    public string Endpoint { get; init; } = "localhost:9000";

    /// <summary>
    ///     对象预览域名
    /// </summary>
    public string PublicUrl { get; init; } = string.Empty;

    /// <summary>
    ///     MinIO 私密密钥
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    ///     是否使用 HTTPS
    /// </summary>
    public bool UseSsl { get; init; }
}