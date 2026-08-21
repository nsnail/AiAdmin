// 提供文件管理所需的 MinIO 对象存储操作

using System.Globalization;
using Minio;
using Minio.DataModel.Args;

namespace AiAdmin.Api.Services;

/// <summary>
///     MinIO 文件存储服务
/// </summary>
public sealed class MinioStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MinioStorageService" /> class.
    ///     初始化 MinIO 存储客户端
    /// </summary>
    /// <param name="configuration">应用配置</param>
    public MinioStorageService(IConfiguration configuration) {
        _options = configuration.GetSection("Minio").Get<MinioOptions>() ?? new MinioOptions();
        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSsl)
            .Build();
    }

    /// <summary>
    ///     创建 MinIO 虚拟目录
    /// </summary>
    /// <param name="objectName">目录对象名称</param>
    /// <returns>异步操作任务</returns>
    public async Task CreateDirectoryAsync(string objectName) {
        await EnsureBucketAsync().ConfigureAwait(false);

        // MinIO SDK 不接受零长度 PutObject，使用一个不可见的占位字节表示虚拟目录
        await using var stream = new MemoryStream([0]);
        _ = await _client
            .PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectName.TrimEnd('/') + "/")
                    .WithStreamData(stream)
                    .WithObjectSize(1)
                    .WithContentType("application/x-directory")
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     删除 MinIO 文件对象
    /// </summary>
    /// <param name="objectName">对象名称</param>
    /// <returns>异步操作任务</returns>
    public Task DeleteAsync(string objectName) {
        return _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.Bucket).WithObject(objectName));
    }

    /// <summary>
    ///     递归删除 MinIO 虚拟目录及其全部对象
    /// </summary>
    /// <param name="prefix">目录对象前缀</param>
    /// <returns>异步操作任务</returns>
    public async Task DeleteDirectoryAsync(string prefix) {
        await EnsureBucketAsync().ConfigureAwait(false);
        var keys = new List<string>();
        await foreach (var item in _client
                           .ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(_options.Bucket).WithPrefix(prefix).WithRecursive(true))
                           .ConfigureAwait(false)) {
            keys.Add(item.Key);
        }

        foreach (var key in keys) {
            await DeleteAsync(key).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     创建文件临时下载地址
    /// </summary>
    /// <param name="objectName">对象名称</param>
    /// <returns>临时下载地址</returns>
    public Task<string> GetDownloadUrlAsync(string objectName) {
        return _client.PresignedGetObjectAsync(new PresignedGetObjectArgs().WithBucket(_options.Bucket).WithObject(objectName).WithExpiry(3600));
    }

    /// <summary>
    ///     获取对象预览地址
    /// </summary>
    /// <param name="objectName">对象名称</param>
    /// <returns>对象预览地址或对象路径</returns>
    public string GetPreviewUrl(string objectName) {
        return string.IsNullOrWhiteSpace(_options.PublicUrl) ? objectName : $"{_options.PublicUrl.TrimEnd('/')}/{objectName.TrimStart('/')}";
    }

    /// <summary>
    ///     列出指定目录下的直接子项
    /// </summary>
    /// <param name="prefix">用户目录前缀</param>
    /// <param name="keyword">文件或目录名称关键词</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="descending">是否倒序</param>
    /// <returns>文件和目录列表</returns>
    public async Task<IReadOnlyList<MinioObject>> ListAsync(
        string prefix
        , string? keyword
        , string sortField = "Name"
        , bool descending = false
    ) {
        await EnsureBucketAsync().ConfigureAwait(false);
        var result = new List<MinioObject>();
        var entries = new Dictionary<string, MinioObject>(StringComparer.Ordinal);
        await foreach (var item in _client
                           .ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(_options.Bucket).WithPrefix(prefix).WithRecursive(true))
                           .ConfigureAwait(false)) {
            var key = item.Key;
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var remainder = key[prefix.Length..];
            if (string.IsNullOrEmpty(remainder)) {
                continue;
            }

            var separator = remainder.IndexOf('/');
            var entryName = separator < 0 ? remainder : remainder[..(separator + 1)];
            var entryKey = prefix + entryName;
            var isDirectory = separator >= 0 || item.IsDir;
            entries[entryKey] = isDirectory
                ? new MinioObject(entryKey, 0, DateTime.Parse(item.LastModified, CultureInfo.InvariantCulture), true)
                : new MinioObject(entryKey, checked((long)item.Size), DateTime.Parse(item.LastModified, CultureInfo.InvariantCulture));
        }

        result.AddRange(
            entries.Values.Where(x => string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
        );
        IEnumerable<MinioObject> ordered = sortField.ToLowerInvariant() switch
        {
            "size" => descending ? result.OrderByDescending(x => x.Size) : result.OrderBy(x => x.Size)
            , "lastmodified" => descending ? result.OrderByDescending(x => x.LastModified) : result.OrderBy(x => x.LastModified)
            , _ => descending ? result.OrderByDescending(x => x.Name, StringComparer.Ordinal) : result.OrderBy(x => x.Name, StringComparer.Ordinal)
        };
        return [.. ordered];
    }

    /// <summary>
    ///     上传文件到 MinIO
    /// </summary>
    /// <param name="objectName">对象名称</param>
    /// <param name="stream">文件流</param>
    /// <param name="size">文件大小</param>
    /// <param name="contentType">文件类型</param>
    /// <returns>异步操作任务</returns>
    public async Task UploadAsync(
        string objectName
        , Stream stream
        , long size
        , string contentType
    ) {
        await EnsureBucketAsync().ConfigureAwait(false);
        _ = await _client
            .PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(size)
                    .WithContentType(contentType)
            )
            .ConfigureAwait(false);
    }

    private async Task EnsureBucketAsync() {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.Bucket)).ConfigureAwait(false);
        if (!exists) {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.Bucket)).ConfigureAwait(false);
        }
    }
}