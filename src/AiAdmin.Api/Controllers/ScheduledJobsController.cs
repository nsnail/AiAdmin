using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     计划作业管理控制器
/// </summary>
/// <param name="db">数据库上下文</param>
/// <param name="lockService">计划作业分布式锁服务</param>
[ApiController]
[Authorize]
[Route("api/scheduled-job")]
[ApiDescription("Scheduled job management")]
public sealed class ScheduledJobsController(AppDbContext db, ScheduledJobLockService lockService) : ControllerBase
{
    /// <summary>
    ///     新增计划作业
    /// </summary>
    /// <param name="request">作业保存请求</param>
    /// <returns>新增作业</returns>
    [HttpPost]
    [ApiDescription("Create scheduled job")]
    public Task<ActionResult<ApiResponse<ScheduledJobResult>>> CreateAsync(SaveScheduledJobRequest request) {
        return SaveAsync(null, request);
    }

    /// <summary>
    ///     删除计划作业
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete scheduled job")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var job = await db.ScheduledJobs.FindAsync(id).ConfigureAwait(false);
        if (job is null) {
            return NotFound(new ApiResponse<object>(404, "Scheduled job not found", null));
        }

        _ = db.ScheduledJobs.Remove(job);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     分页查询作业执行记录
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <param name="request">包含筛选、排序和分页信息的请求体</param>
    /// <returns>执行记录分页结果</returns>
    [HttpPost("{id:long}/executions/list")]
    [ApiDescription("Query scheduled job execution list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobExecutionResult>>>> ExecutionListAsync(
        long id
        , [FromBody] DynamicQueryRequest request
    ) {
        var query = db.ScheduledJobExecutions.AsNoTracking().Where(x => x.ScheduledJobId == id).ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(ScheduledJobExecution.StartedAt), true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        var results = rows.ConvertAll(x => new ScheduledJobExecutionResult(
                x.Id, x.ScheduledJobId, ServerTime.ToOffset(x.CreatedAt), x.UpdatedAt.HasValue ? ServerTime.ToOffset(x.UpdatedAt.Value) : null
                , ServerTime.ToOffset(x.StartedAt), x.FinishedAt.HasValue ? ServerTime.ToOffset(x.FinishedAt.Value) : null, x.RequestUrl
                , x.RequestMethod
                , x.RequestHeaders, x.RequestBody, x.ResponseStatusCode, x.ResponseHeaders, x.ResponseBody, x.Status, x.ErrorMessage
            )
        );
        return Ok(
            ApiResponse<PagedResponse<ScheduledJobExecutionResult>>.Ok(
                new PagedResponse<ScheduledJobExecutionResult>(results, request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     查询作业执行记录
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <returns>执行记录分页结果</returns>
    [HttpGet("{id:long}/executions")]
    [ApiDescription("Query scheduled job executions")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobExecutionResult>>>> ExecutionsAsync(
        long id
        , int current = 1
        , int size = 20
    ) {
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 100);
        var query = db.ScheduledJobExecutions.AsNoTracking().Where(x => x.ScheduledJobId == id).OrderByDescending(x => x.StartedAt);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query.Skip((current - 1) * size).Take(size).ToListAsync().ConfigureAwait(false);
        var results = rows.ConvertAll(x => new ScheduledJobExecutionResult(
                x.Id, x.ScheduledJobId, ServerTime.ToOffset(x.CreatedAt), x.UpdatedAt.HasValue ? ServerTime.ToOffset(x.UpdatedAt.Value) : null
                , ServerTime.ToOffset(x.StartedAt), x.FinishedAt.HasValue ? ServerTime.ToOffset(x.FinishedAt.Value) : null, x.RequestUrl
                , x.RequestMethod
                , x.RequestHeaders, x.RequestBody, x.ResponseStatusCode, x.ResponseHeaders, x.ResponseBody, x.Status, x.ErrorMessage
            )
        );
        return Ok(
            ApiResponse<PagedResponse<ScheduledJobExecutionResult>>.Ok(new PagedResponse<ScheduledJobExecutionResult>(results, current, size, total))
        );
    }

    /// <summary>
    ///     查询计划作业列表筛选字段元数据
    /// </summary>
    /// <returns>计划作业筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query scheduled job filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<ScheduledJob>()));
    }

    /// <summary>
    ///     分页查询计划作业
    /// </summary>
    /// <param name="request">包含动态筛选和分页信息的请求体</param>
    /// <returns>计划作业分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query scheduled job list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.ScheduledJobs.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(ScheduledJob.CreatedAt), true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<ScheduledJobResult>>.Ok(
                new PagedResponse<ScheduledJobResult>(rows.ConvertAll(ToResult), request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     立即执行指定作业
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <returns>执行结果</returns>
    [HttpPost("{id:long}/run")]
    [ApiDescription("Run scheduled job")]
    public async Task<ActionResult<ApiResponse<object>>> RunAsync(long id) {
        await using var jobLock = await lockService.TryAcquireAsync(id, TimeSpan.FromSeconds(2), HttpContext.RequestAborted).ConfigureAwait(false);
        if (jobLock is null) {
            return Conflict(new ApiResponse<object>(409, "Scheduled job is being updated", null));
        }

        var job = await db.ScheduledJobs.FindAsync(id).ConfigureAwait(false);
        if (job is null) {
            return NotFound(new ApiResponse<object>(404, "Scheduled job not found", null));
        }

        if (job.Status == ScheduledJobStatus.Running) {
            return Conflict(new ApiResponse<object>(409, "Scheduled job is running", null));
        }

        job.LastTriggeredAt = null;
        job.Status = ScheduledJobStatus.Waiting;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     修改计划作业
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <param name="request">作业保存请求</param>
    /// <returns>修改后的作业</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update scheduled job")]
    public Task<ActionResult<ApiResponse<ScheduledJobResult>>> UpdateAsync(
        long id
        , SaveScheduledJobRequest request
    ) {
        return SaveAsync(id, request);
    }

    private static ScheduledJobResult ToResult(ScheduledJob x) {
        return new ScheduledJobResult(
            x.Id, ServerTime.ToOffset(x.CreatedAt), x.Name, x.CronExpression, x.RequestUrl, x.RequestMethod, x.RequestHeadersJson, x.RequestBody
            , x.TimeoutSeconds, x.IsEnabled, x.Status, x.LastTriggeredAt.HasValue ? ServerTime.ToOffset(x.LastTriggeredAt.Value) : null
            , x.LastFinishedAt.HasValue ? ServerTime.ToOffset(x.LastFinishedAt.Value) : null, x.LastError
        );
    }

    private async Task<ActionResult<ApiResponse<ScheduledJobResult>>> SaveAsync(
        long? id
        , SaveScheduledJobRequest request
    ) {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.CronExpression)
            || string.IsNullOrWhiteSpace(request.RequestUrl)) {
            return BadRequest(new ApiResponse<object>(400, "Name, cron expression and URL are required", null));
        }

        var job = id.HasValue
            ? await db.ScheduledJobs.FindAsync(id.Value).ConfigureAwait(false)
            : new ScheduledJob { Name = request.Name.Trim(), CronExpression = request.CronExpression.Trim(), RequestUrl = request.RequestUrl.Trim() };
        if (job is null) {
            return NotFound(new ApiResponse<object>(404, "Scheduled job not found", null));
        }

        job.Name = request.Name.Trim();
        job.CronExpression = request.CronExpression.Trim();
        job.RequestUrl = request.RequestUrl.Trim();
        job.RequestMethod = request.RequestMethod.Trim().ToUpperInvariant();
        job.RequestHeadersJson = request.RequestHeadersJson;
        job.RequestBody = request.RequestBody;
        job.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 1, 86400);
        job.IsEnabled = request.IsEnabled;
        if (!id.HasValue) {
            _ = await db.ScheduledJobs.AddAsync(job).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<ScheduledJobResult>.Ok(ToResult(job)));
    }
}