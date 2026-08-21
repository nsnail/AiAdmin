using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     系统日志管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/system-log")]
[ApiDescription("System log management")]
public sealed class SystemLogsController(ElasticsearchLogQueryService queryService) : ControllerBase
{
    /// <summary>
    ///     分页查询 Elasticsearch 系统日志
    /// </summary>
    /// <param name="request">日志分页查询请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>系统日志分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query system log list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SystemLogItem>>>> ListAsync(
        [FromBody] SystemLogQueryRequest request
        , CancellationToken cancellationToken
    ) {
        request.Current = Math.Max(request.Current, 1);
        request.Size = Math.Clamp(request.Size, 1, 100);
        var (records, total) = await queryService.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PagedResponse<SystemLogItem>>.Ok(new PagedResponse<SystemLogItem>(records, request.Current, request.Size, total)));
    }
}