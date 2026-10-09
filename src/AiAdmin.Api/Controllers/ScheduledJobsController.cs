using System.Data;
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
    ///     批量更新计划作业备注
    /// </summary>
    /// <param name="request">批量备注请求</param>
    /// <returns>更新结果</returns>
    [HttpPost("batch-remark")]
    [ApiDescription("Batch update scheduled job remarks")]
    public async Task<ActionResult<ApiResponse<object>>> BatchUpdateRemarkAsync(BatchUpdateScheduledJobRemarkRequest request) {
        var ids = request.Ids.Distinct().ToArray();
        var remark = request.Remark.Trim();
        var affected = await db
            .ScheduledJobs.Where(x => Enumerable.Contains(ids, x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(row => row.Remark, remark).SetProperty(row => row.UpdatedAt, DateTime.UtcNow))
            .ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { affected }));
    }

    /// <summary>
    ///     清理超过保留时长的数据库审计日志
    /// </summary>
    /// <param name="request">清理参数</param>
    /// <returns>清理结果</returns>
    [HttpPost("database-audit-logs/cleanup")]
    [ApiDescription("Cleanup database audit logs")]
    public async Task<ActionResult<ApiResponse<CleanupDatabaseAuditLogsResult>>> CleanupDatabaseAuditLogsAsync(
        CleanupDatabaseAuditLogsRequest request
    ) {
        var cutoff = DateTime.UtcNow.AddHours(-request.Hours);
        var deletedCount = await db.DatabaseAuditLogs.Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync().ConfigureAwait(false);
        return Ok(ApiResponse<CleanupDatabaseAuditLogsResult>.Ok(new CleanupDatabaseAuditLogsResult(deletedCount, ServerTime.ToOffset(cutoff))));
    }

    /// <summary>
    ///     清理超过保留时长的计划作业执行记录
    /// </summary>
    /// <param name="request">清理参数</param>
    /// <returns>清理结果</returns>
    [HttpPost("executions/cleanup")]
    [ApiDescription("Cleanup scheduled job executions")]
    public async Task<ActionResult<ApiResponse<CleanupScheduledJobExecutionsResult>>> CleanupExecutionsAsync(
        CleanupScheduledJobExecutionsRequest request
    ) {
        var cutoff = DateTime.UtcNow.AddHours(-request.Hours);
        var deletedCount = await db.ScheduledJobExecutions.Where(x => x.StartedAt < cutoff).ExecuteDeleteAsync().ConfigureAwait(false);
        return Ok(
            ApiResponse<CleanupScheduledJobExecutionsResult>.Ok(new CleanupScheduledJobExecutionsResult(deletedCount, ServerTime.ToOffset(cutoff)))
        );
    }

    /// <summary>
    ///     复制计划作业
    /// </summary>
    /// <param name="request">源作业标识请求</param>
    /// <returns>复制后的作业</returns>
    [HttpPost("copy")]
    [ApiDescription("Copy scheduled job")]
    public async Task<ActionResult<ApiResponse<ScheduledJobResult>>> CopyAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
        var source = await db.ScheduledJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (source is null) {
            return NotFound(new ApiResponse<object>(404, "Scheduled job not found", null));
        }

        var copy = new ScheduledJob
        {
            Name = $"{source.Name} - Copy"
            , CronExpression = source.CronExpression
            , RequestUrl = source.RequestUrl
            , RequestMethod = source.RequestMethod
            , RequestHeadersJson = source.RequestHeadersJson
            , RequestBody = source.RequestBody
            , Remark = source.Remark
            , TimeoutSeconds = source.TimeoutSeconds
            , IsEnabled = false
            , Status = ScheduledJobStatus.Waiting
        };
        _ = await db.ScheduledJobs.AddAsync(copy).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<ScheduledJobResult>.Ok(ToResult(copy)));
    }

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
    /// <param name="request">作业标识请求</param>
    /// <returns>删除结果</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete scheduled job")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
        var job = await db.ScheduledJobs.FindAsync(id).ConfigureAwait(false);
        if (job is null) {
            return NotFound(new ApiResponse<object>(404, "Scheduled job not found", null));
        }

        _ = db.ScheduledJobs.Remove(job);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     查询作业执行记录筛选字段元数据
    /// </summary>
    /// <param name="id">作业主键</param>
    /// <returns>执行记录筛选字段定义</returns>
    [HttpGet("executions/filter-fields")]
    [ApiDescription("Query scheduled job execution filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> ExecutionFilterFields([FromQuery] long id) {
        _ = id;
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<ScheduledJobExecution>()));
    }

    /// <summary>
    ///     查询当前作业执行记录筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    [HttpPost("executions/filter-groups")]
    [ApiDescription("Query scheduled job execution filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> ExecutionFilterGroupsAsync(
        [FromBody] ListFilterGroupRequest request
    ) {
        var id = request.ParentId.GetValueOrDefault();
        var groups = await ListFilterGroupingService
            .GetGroupsAsync(db.ScheduledJobExecutions.AsNoTracking().Where(x => x.ScheduledJobId == id), request.DynamicFilter)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询作业执行记录
    /// </summary>
    /// <param name="request">包含筛选、排序和分页信息的请求体</param>
    /// <returns>执行记录分页结果</returns>
    [HttpPost("executions/list")]
    [ApiDescription("Query scheduled job execution list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobExecutionResult>>>> ExecutionListAsync(
        [FromBody] DynamicQueryRequest request
    ) {
        var id = request.ParentId.GetValueOrDefault();
        var query = db.ScheduledJobExecutions.AsNoTracking().Where(x => x.ScheduledJobId == id).ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(ScheduledJobExecution.StartedAt), true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        var results = rows.ConvertAll(x => new ScheduledJobExecutionResult(
                x.Id, x.ScheduledJobId, ServerTime.ToOffset(x.CreatedAt), ServerTime.ToOffset(x.StartedAt), x.RequestUrl, x.RequestMethod
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
    [HttpGet("executions")]
    [ApiDescription("Query scheduled job executions")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobExecutionResult>>>> ExecutionsAsync(
        [FromQuery] long id
        , int current = 1
        , int size = 20
    ) {
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 100);
        var query = db.ScheduledJobExecutions.AsNoTracking().Where(x => x.ScheduledJobId == id).OrderByDescending(x => x.StartedAt);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query.Skip((current - 1) * size).Take(size).ToListAsync().ConfigureAwait(false);
        var results = rows.ConvertAll(x => new ScheduledJobExecutionResult(
                x.Id, x.ScheduledJobId, ServerTime.ToOffset(x.CreatedAt), ServerTime.ToOffset(x.StartedAt), x.RequestUrl, x.RequestMethod
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
    ///     查询当前作业筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query scheduled job filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted).ConfigureAwait(false);
        var groups = await ListFilterGroupingService.GetGroupsAsync(db.ScheduledJobs.AsNoTracking(), request.DynamicFilter).ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询计划作业
    /// </summary>
    /// <param name="request">包含动态筛选和分页信息的请求体</param>
    /// <returns>计划作业分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query scheduled job list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ScheduledJobResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted).ConfigureAwait(false);
        var query = db.ScheduledJobs.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        List<ScheduledJob> rows;
        if (string.Equals(request.SortField, "executionDuration", StringComparison.OrdinalIgnoreCase)) {
            // 时长是两个时间字段的计算值；仅取排序所需字段，再按分页结果读取完整作业
            var durations = await query.Select(x => new { x.Id, x.LastTriggeredAt, x.LastFinishedAt }).ToListAsync().ConfigureAwait(false);
            var ascending = request.SortOrder?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true
                            || request.SortOrder?.Equals("ascending", StringComparison.OrdinalIgnoreCase) == true;
            var ordered = ascending
                ? durations
                    .OrderBy(x => x.LastTriggeredAt.HasValue && x.LastFinishedAt.HasValue
                        ? Math.Max(0, (x.LastFinishedAt.Value - x.LastTriggeredAt.Value).Ticks)
                        : 0
                    )
                    .ThenBy(x => x.Id)
                : durations
                    .OrderByDescending(x =>
                        x.LastTriggeredAt.HasValue && x.LastFinishedAt.HasValue
                            ? Math.Max(0, (x.LastFinishedAt.Value - x.LastTriggeredAt.Value).Ticks)
                            : 0
                    )
                    .ThenBy(x => x.Id);
            var pageIds = ordered.Skip((request.Current - 1) * request.Size).Take(request.Size).Select(x => x.Id).ToArray();
            var pageRows = await query.Where(x => pageIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id).ConfigureAwait(false);
            rows = [.. pageIds.Select(id => pageRows[id])];
        }
        else {
            rows = await query
                .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(ScheduledJob.CreatedAt), true)
                .Skip((request.Current - 1) * request.Size)
                .Take(request.Size)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<ScheduledJobResult>>.Ok(
                new PagedResponse<ScheduledJobResult>(rows.ConvertAll(ToResult), request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     立即执行指定作业
    /// </summary>
    /// <param name="request">作业标识请求</param>
    /// <returns>执行结果</returns>
    [HttpPost("run")]
    [ApiDescription("Run scheduled job")]
    public async Task<ActionResult<ApiResponse<object>>> RunAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
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
    /// <param name="request">作业保存请求</param>
    /// <returns>修改后的作业</returns>
    [HttpPost("update")]
    [ApiDescription("Update scheduled job")]
    public Task<ActionResult<ApiResponse<ScheduledJobResult>>> UpdateAsync([FromBody] SaveScheduledJobRequest request) {
        return SaveAsync(request.Id, request);
    }

    /// <summary>
    ///     转换 ToResult 方法对应的业务数据
    /// </summary>
    /// <param name="x">方法参数 x</param>
    /// <returns>ToResult 方法的执行结果</returns>
    private static ScheduledJobResult ToResult(ScheduledJob x) {
        return new ScheduledJobResult(
            x.Id, ServerTime.ToOffset(x.CreatedAt), x.Name, x.CronExpression, x.RequestUrl, x.RequestMethod, x.RequestHeadersJson, x.RequestBody
            , x.Remark, x.TimeoutSeconds, x.IsEnabled, x.Status, x.LastTriggeredAt.HasValue ? ServerTime.ToOffset(x.LastTriggeredAt.Value) : null
            , x.LastFinishedAt.HasValue ? ServerTime.ToOffset(x.LastFinishedAt.Value) : null, x.LastError
        );
    }

    /// <summary>
    ///     保存 SaveAsync 方法对应的业务数据
    /// </summary>
    /// <param name="id">实体标识</param>
    /// <param name="request">请求参数</param>
    /// <returns>SaveAsync 方法的执行结果</returns>
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
        job.Remark = request.Remark.Trim();
        job.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 1, 86400);
        job.IsEnabled = request.IsEnabled;
        if (!id.HasValue) {
            _ = await db.ScheduledJobs.AddAsync(job).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<ScheduledJobResult>.Ok(ToResult(job)));
    }
}