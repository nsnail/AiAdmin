using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     系统日志管理控制器
/// </summary>
/// <param name="queryService">Elasticsearch 日志查询服务</param>
[ApiController]
[Route("api/system-log")]
[ApiDescription("System log management")]
public sealed class SystemLogsController(ElasticsearchLogQueryService queryService) : ControllerBase
{
    /// <summary>
    ///     Elasticsearch 结果窗口上限，导出条数不能超过该值
    /// </summary>
    private const int _MAX_RESULT_WINDOW = 10000;

    /// <summary>
    ///     导出时每次向 Elasticsearch 读取的条数，与公共查询的单页上限一致
    /// </summary>
    private const int _SEARCH_PAGE_SIZE = 100;

    /// <summary>
    ///     导出系统日志，未勾选时导出当前查询条件和排序下的全部结果，受系统设置的导出上限约束
    /// </summary>
    /// <param name="request">动态筛选与排序请求</param>
    /// <param name="limits">导出上限服务</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>受系统设置条数限制的系统日志导出结果</returns>
    [HttpPost("export")]
    [ApiDescription("Export system log data")]
    public async Task<ActionResult<ApiResponse<DynamicExportResult<SystemLogItem>>>> ExportAsync(
        [FromBody] DynamicExportRequest request
        , [FromServices] ExportLimitService limits
        , CancellationToken cancellationToken
    ) {
        var limit = Math.Min(await limits.GetLimitAsync().ConfigureAwait(false), _MAX_RESULT_WINDOW);
        var records = new List<SystemLogItem>();
        var total = 0;
        var current = 1;
        var exhausted = false;
        while (!exhausted && records.Count < limit) {
            var query = new DynamicQueryRequest
            {
                Current = current
                , Size = _SEARCH_PAGE_SIZE
                , SortField = request.SortField
                , SortOrder = request.SortOrder
                , DynamicFilter = request.DynamicFilter
            };
            var (batch, matched) = await queryService.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            total = matched;
            records.AddRange(batch.Take(limit - records.Count));

            // 没有更多数据，或已读完全部匹配记录时结束
            exhausted = batch.Count == 0 || records.Count >= total;
            ++current;
        }

        return Ok(ApiResponse<DynamicExportResult<SystemLogItem>>.Ok(new DynamicExportResult<SystemLogItem>(records, limit, total)));
    }

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