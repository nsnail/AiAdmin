using System.Reflection;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     更新日志控制器，读取编译时嵌入程序集的 CHANGELOG.md
/// </summary>
[ApiController]
[ApiDescription("Change log")]
[Authorize]
[Route("api/change-log")]
public sealed class ChangeLogController : ControllerBase
{
    /// <summary>
    ///     嵌入资源的逻辑名称，与项目文件中的 LogicalName 保持一致
    /// </summary>
    private const string _RESOURCE_NAME = "CHANGELOG.md";

    /// <summary>
    ///     嵌入资源在进程生命周期内不会变化，首次读取后缓存
    /// </summary>
    private static readonly Lazy<string> _content = new(ReadEmbeddedContent);

    /// <summary>
    ///     查询更新日志
    /// </summary>
    /// <returns>更新日志的 Markdown 原文</returns>
    [HttpGet("content")]
    [ApiDescription("Query change log")]
    public ActionResult<ApiResponse<ChangeLogResult>> Content() {
        return Ok(ApiResponse<ChangeLogResult>.Ok(new ChangeLogResult(_content.Value)));
    }

    /// <summary>
    ///     从当前程序集读取嵌入的更新日志，资源不存在时返回空字符串
    /// </summary>
    /// <returns>更新日志文本</returns>
    private static string ReadEmbeddedContent() {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_RESOURCE_NAME);
        if (stream is null) {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}