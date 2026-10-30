using System.Globalization;
using Minio;
using Minio.DataModel;
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
            var entry = ToDirectChild(item, prefix);
            if (entry is not null) {
                entries[entry.Name] = entry;
            }
        }

        result.AddRange(
            entries.Values.Where(x => string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
        );
        return [.. OrderObjects(result, sortField, descending)];
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

    /// <summary>
    ///     按字段对文件和目录排序，未识别的字段按名称排序
    /// </summary>
    /// <param name="items">文件和目录列表</param>
    /// <param name="sortField">排序字段，支持 Size、LastModified 和 Name</param>
    /// <param name="descending">是否倒序</param>
    /// <returns>排序后的文件和目录</returns>
    private static IEnumerable<MinioObject> OrderObjects(
        List<MinioObject> items
        , string sortField
        , bool descending
    ) {
        return sortField.ToLowerInvariant() switch
        {
            "size" => descending ? items.OrderByDescending(x => x.Size) : items.OrderBy(x => x.Size)
            , "lastmodified" => descending ? items.OrderByDescending(x => x.LastModified) : items.OrderBy(x => x.LastModified)
            , _ => descending ? items.OrderByDescending(x => x.Name, StringComparer.Ordinal) : items.OrderBy(x => x.Name, StringComparer.Ordinal)
        };
    }

    /// <summary>
    ///     把前缀下的任一对象折叠为其所在层级的直接子项，位于子目录内的对象折叠为目录项
    /// </summary>
    /// <param name="item">MinIO 返回的对象</param>
    /// <param name="prefix">用户目录前缀</param>
    /// <returns>直接子项，对象不在前缀下或与前缀本身相同时返回 null</returns>
    private static MinioObject? ToDirectChild(
        Item item
        , string prefix
    ) {
        var key = item.Key;
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) {
            return null;
        }

        var remainder = key[prefix.Length..];
        if (string.IsNullOrEmpty(remainder)) {
            return null;
        }

        var separator = remainder.IndexOf('/');
        var entryName = separator < 0 ? remainder : remainder[..(separator + 1)];
        var entryKey = prefix + entryName;
        var lastModified = DateTime.Parse(item.LastModified, CultureInfo.InvariantCulture);
        return separator >= 0 || item.IsDir
            ? new MinioObject(entryKey, 0, lastModified, true)
            : new MinioObject(entryKey, checked((long)item.Size), lastModified);
    }

    /// <summary>
    ///     确保 EnsureBucketAsync 方法对应的业务数据
    /// </summary>
    private async Task EnsureBucketAsync() {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.Bucket)).ConfigureAwait(false);
        if (!exists) {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.Bucket)).ConfigureAwait(false);
        }
    }
}