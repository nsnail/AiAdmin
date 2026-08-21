#pragma warning disable SA1210, SA1518, IDE0022, IDE0011, SA1503, SA1625, S5693

using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     文件管理控制器
/// </summary>
[ApiController]
[ApiDescription("File management")]
[Authorize]
[Route("api/files")]
public sealed class FilesController(MinioStorageService storage) : ControllerBase
{
    private const long _MAX_UPLOAD_SIZE = 100 * 1024 * 1024;

    /// <summary>
    ///     创建文件目录
    /// </summary>
    /// <param name="request">目录创建请求</param>
    /// <returns>创建结果</returns>
    [HttpPost("directories")]
    [ApiDescription("Create file directory")]
    public async Task<ActionResult<ApiResponse<object>>> CreateDirectoryAsync(CreateFileDirectoryRequest request) {
        var path = NormalizePath(request.Path);
        var name = SanitizeSegment(request.Name);
        if (string.IsNullOrEmpty(name) || name is "." or "..") {
            return BadRequest(new ApiResponse<object>(400, "Directory name is invalid", null));
        }

        await storage.CreateDirectoryAsync(UserPrefix() + path + name).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     删除文件
    /// </summary>
    /// <param name="name">对象名称</param>
    /// <param name="directory">是否递归删除目录</param>
    /// <returns>删除结果</returns>
    [HttpDelete]
    [ApiDescription("Delete file")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(
        [FromQuery] string name
        , [FromQuery] bool directory = false
    ) {
        var objectName = UserPrefix() + NormalizeObjectPath(name);
        if (directory || name.EndsWith('/')) {
            await storage.DeleteDirectoryAsync(objectName.TrimEnd('/') + "/").ConfigureAwait(false);
        }
        else {
            await storage.DeleteAsync(objectName).ConfigureAwait(false);
        }

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     通过 JSON 请求删除文件或目录
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <returns>删除结果</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete file or directory")]
    public Task<ActionResult<ApiResponse<object>>> DeleteByRequestAsync(DeleteFileRequest request) {
        return DeleteAsync(request.Name, request.Directory == true);
    }

    /// <summary>
    ///     获取文件下载地址
    /// </summary>
    /// <param name="name">对象名称</param>
    /// <returns>下载地址</returns>
    [HttpGet("download")]
    [ApiDescription("Get file download URL")]
    public async Task<ActionResult<ApiResponse<string>>> DownloadAsync([FromQuery] string name) {
        return Ok(ApiResponse<string>.Ok(await storage.GetDownloadUrlAsync(UserPrefix() + NormalizeObjectPath(name)).ConfigureAwait(false)));
    }

    /// <summary>
    ///     查询文件列表
    /// </summary>
    /// <param name="path">当前目录相对路径</param>
    /// <param name="keyword">文件或目录名称关键词</param>
    /// <param name="sortField">排序字段，支持 Name、Size、LastModified</param>
    /// <param name="sortOrder">排序顺序，支持 ascending、descending</param>
    /// <returns>文件列表</returns>
    [HttpGet]
    [ApiDescription("Query file list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MinioObject>>>> ListAsync(
        [FromQuery] string? path
        , [FromQuery] string? keyword
        , [FromQuery] string? sortField
        , [FromQuery] string? sortOrder
    ) {
        var relativePath = NormalizePath(path);
        var descending = string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var items = await storage.ListAsync(UserPrefix() + relativePath, keyword, sortField ?? "Name", descending).ConfigureAwait(false);
        var listingPrefix = UserPrefix() + relativePath;
        return Ok(ApiResponse<IReadOnlyList<MinioObject>>.Ok([.. items.Select(x => x with { Name = x.Name[listingPrefix.Length..] })]));
    }

    /// <summary>
    ///     上传文件
    /// </summary>
    /// <param name="file">上传文件</param>
    /// <param name="path">当前目录相对路径</param>
    /// <returns>文件对象信息</returns>
    [HttpPost("upload")]
    [ApiDescription("Upload file")]
    [RequestSizeLimit(_MAX_UPLOAD_SIZE)]
    public async Task<ActionResult<ApiResponse<MinioObject>>> UploadAsync(
        IFormFile file
        , [FromQuery] string? path
    ) {
        switch (file.Length) {
            // 限制单个文件大小，避免超大请求占用过多内存和存储资源
            case 0:
                return BadRequest(new ApiResponse<object>(400, "File is empty", null));
            case > _MAX_UPLOAD_SIZE:
                return BadRequest(new ApiResponse<object>(400, "File must not exceed 100 MB", null));
        }

        var normalizedPath = NormalizePath(path);
        var originalName = Path.GetFileName(file.FileName);
        var baseName = SanitizeSegment(Path.GetFileNameWithoutExtension(originalName));
        var extension = Path.GetExtension(originalName);
        var name = $"{UserPrefix()}{normalizedPath}{baseName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{extension}";
        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(name, stream, file.Length, file.ContentType).ConfigureAwait(false);
        return Ok(ApiResponse<MinioObject>.Ok(new MinioObject(normalizedPath + Path.GetFileName(name), file.Length, DateTime.UtcNow)));
    }

    private static string NormalizeObjectPath(string? path) {
        return NormalizePath(path).TrimEnd('/');
    }

    private static string NormalizePath(string? path) {
        var value = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (value.Length == 0) {
            return string.Empty;
        }

        var segments = value
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeSegment)
            .Where(x => x.Length > 0 && x is not "." and not "..");
        return string.Join('/', segments) + "/";
    }

    private static string SanitizeSegment(string value) {
        return string.Join(string.Empty, value.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch) && ch != '/' && ch != '\\')).Trim();
    }

    // MinIO 对象键不使用开头斜杠，控制台 URL 会将该键显示为 /users/{userid}/...
    // 超级管理员使用桶根目录，其他用户固定使用自己的 users/{userid}/ 目录
    private string UserPrefix() {
        return User.IsInRole("R_SUPER")
            ? string.Empty
            : $"users/{User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User identity is missing")}/";
    }
}