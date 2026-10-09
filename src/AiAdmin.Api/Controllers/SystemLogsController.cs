using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     系统日志管理控制器
/// </summary>
/// <param name="queryService">Elasticsearch 日志查询服务</param>
[ApiController]
[Authorize]
[Route("api/system-log")]
[ApiDescription("System log management")]
public sealed class SystemLogsController(ElasticsearchLogQueryService queryService) : ControllerBase
{
    /// <summary>
    ///     获取系统日志筛选字段元数据
    /// </summary>
    /// <returns>日志字段的控件和值类型定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Get system log filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> GetFilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<SystemLogItem>()));
    }

    /// <summary>
    ///     分页查询 Elasticsearch 系统日志
    /// </summary>
    /// <param name="request">日志分页查询请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>系统日志分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query system log list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SystemLogItem>>>> ListAsync(
        [FromBody] DynamicQueryRequest request
        , CancellationToken cancellationToken
    ) {
        var (records, total) = await queryService.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PagedResponse<SystemLogItem>>.Ok(new PagedResponse<SystemLogItem>(records, request.Current, request.Size, total)));
    }
}