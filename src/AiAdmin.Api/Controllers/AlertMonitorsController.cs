using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     告警监控管理控制器：监控字典内容或 Redis 缓存键的值，命中条件时报警，另提供供计划作业定期调用的扫描接口
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="scanService">告警监控扫描服务</param>
[ApiController]
[ApiDescription("Alert monitor management")]
[Route("api/alert-monitor")]
public sealed class AlertMonitorsController(AppDbContext db, AlertMonitorScanService scanService) : ControllerBase
{
    /// <summary>
    ///     新增告警监控
    /// </summary>
    /// <param name="request">告警监控保存请求</param>
    /// <returns>新增后的告警监控</returns>
    [HttpPost]
    [ApiDescription("Create alert monitor")]
    public Task<ActionResult<ApiResponse<AlertMonitorListItem>>> CreateAsync([FromBody] SaveAlertMonitorRequest request) {
        return SaveAsync(null, request);
    }

    /// <summary>
    ///     删除告警监控，单条和批量共用
    /// </summary>
    /// <param name="request">包含监控主键集合的请求体</param>
    /// <returns>实际删除的监控数量</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete alert monitors")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] DeleteAlertMonitorRequest request) {
        var ids = request.Ids.Distinct().ToList();
        var rows = await db.AlertMonitors.Where(x => ids.Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        if (rows.Count != ids.Count) {
            return NotFound(new ApiResponse<object>(404, "Some alert monitors do not exist", null));
        }

        db.AlertMonitors.RemoveRange(rows);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { count = rows.Count }, "Alert monitors deleted"));
    }

    /// <summary>
    ///     查询告警监控列表筛选字段元数据
    /// </summary>
    /// <returns>告警监控筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query alert monitor filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<AlertMonitor>()));
    }

    /// <summary>
    ///     查询告警监控列表分组统计
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>告警监控列表分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query alert monitor filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService.GetGroupsAsync(db.AlertMonitors.AsNoTracking(), request.DynamicFilter).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     分页查询告警监控列表
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>告警监控分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query alert monitor list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AlertMonitorListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.AlertMonitors.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);

        // 追加主键作为次级排序，保证排序字段取值相同时分页结果稳定
        var sorted = (IOrderedQueryable<AlertMonitor>)query.ApplyDynamicSort(
            request.SortField, request.SortOrder, nameof(AlertMonitor.CreatedAt), true
        );
        var rows = await sorted
            .ThenByKey(x => x.Id, request.SortField, request.SortOrder, true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<AlertMonitorListItem>>.Ok(
                new PagedResponse<AlertMonitorListItem>(rows.ConvertAll(ToListItem), request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     扫描全部已启用的告警监控，供计划作业定期调用：命中条件触发报警，恢复后解除报警，通知发送到系统设置中的报警机器人
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次扫描的统计结果</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpPost("scan")]
    [ApiDescription("Scan alert monitors")]
    public async Task<ActionResult<ApiResponse<AlertMonitorScanResult>>> ScanAsync(CancellationToken cancellationToken) {
        // 扫描会读取并推送所有监控的状态，因此只允许全部数据权限调用
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        var result = await scanService.ScanAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AlertMonitorScanResult>.Ok(result));
    }

    /// <summary>
    ///     更新告警监控的启用状态，停用时清除报警状态，避免再次启用时因旧状态而漏报
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>状态更新结果</returns>
    [HttpPost("enabled")]
    [ApiDescription("Update alert monitor enabled state")]
    public async Task<ActionResult<ApiResponse<object>>> SetEnabledAsync([FromBody] SetEnabledRequest request) {
        var enabled = request.IsEnabled;
        var affected = await db
            .AlertMonitors.Where(x => x.Id == request.Id)
            .ExecuteUpdateAsync(x =>
                x
                    .SetProperty(row => row.IsEnabled, enabled)
                    .SetProperty(row => row.AlertActive, row => enabled && row.AlertActive)
                    .SetProperty(row => row.UpdatedAt, DateTime.UtcNow)
            )
            .ConfigureAwait(false);
        if (affected == 0) {
            return NotFound(new ApiResponse<object>(404, "Resource not found", null));
        }

        return Ok(ApiResponse<object>.Ok(new { request.IsEnabled }, "Enabled state updated"));
    }

    /// <summary>
    ///     修改告警监控
    /// </summary>
    /// <param name="request">告警监控保存请求，必须包含主键</param>
    /// <returns>修改后的告警监控</returns>
    [HttpPost("update")]
    [ApiDescription("Update alert monitor")]
    public Task<ActionResult<ApiResponse<AlertMonitorListItem>>> UpdateAsync([FromBody] SaveAlertMonitorRequest request) {
        return SaveAsync(request.Id, request);
    }

    /// <summary>
    ///     判断保存请求是否改变了判定相关的配置：来源、目标、指标、类型、操作符、阈值或大小写敏感
    /// </summary>
    /// <param name="row">原监控</param>
    /// <param name="request">保存请求</param>
    /// <returns>判定配置发生变化返回 true</returns>
    private static bool IsJudgementChanged(
        AlertMonitor row
        , SaveAlertMonitorRequest request
    ) {
        return row.Source != request.Source
               || row.CategoryCode != (request.Source == AlertMonitorSource.Dictionary ? request.CategoryCode.Trim() : string.Empty)
               || row.Target != request.Target.Trim()
               || row.Metric != request.Metric
               || row.ValueType != request.ValueType
               || row.Operator != request.Operator
               || row.Threshold != request.Threshold.Trim()
               || row.CaseSensitive != request.CaseSensitive;
    }

    /// <summary>
    ///     转换告警监控实体为列表项
    /// </summary>
    /// <param name="row">告警监控实体</param>
    /// <returns>告警监控列表项</returns>
    private static AlertMonitorListItem ToListItem(AlertMonitor row) {
        return new AlertMonitorListItem(
            row.Id, ServerTime.ToOffset(row.CreatedAt), row.Name, row.Source, row.CategoryCode, row.Target, row.Metric, row.ValueType, row.Operator
            , row.Threshold, row.CaseSensitive, row.Mentions, row.Remark, row.IsEnabled, row.AlertActive
            , row.AlertedAt.HasValue ? ServerTime.ToOffset(row.AlertedAt.Value) : null, row.LastValue, row.LastMessage
            , row.LastCheckedAt.HasValue ? ServerTime.ToOffset(row.LastCheckedAt.Value) : null
            , row.UpdatedAt.HasValue ? ServerTime.ToOffset(row.UpdatedAt.Value) : null
        );
    }

    /// <summary>
    ///     校验保存请求，通过返回 null，不通过返回对应的 400 结果
    /// </summary>
    /// <param name="request">告警监控保存请求</param>
    /// <returns>校验不通过返回 400 结果，通过返回 null</returns>
    private static BadRequestObjectResult? Validate(SaveAlertMonitorRequest request) {
        if (!Enum.IsDefined(request.Source)
            || !Enum.IsDefined(request.ValueType)
            || !Enum.IsDefined(request.Operator)
            || !Enum.IsDefined(request.Metric)) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "Alert monitor option is invalid", null));
        }

        if (request.Name.Trim().Length == 0 || request.Target.Trim().Length == 0) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "Name and target are required", null));
        }

        if (request.Source == AlertMonitorSource.Dictionary && request.CategoryCode.Trim().Length == 0) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "Dictionary category code is required", null));
        }

        if (!AlertMonitorEvaluator.IsMetricAllowed(request.Source, request.Metric, request.ValueType)) {
            return new BadRequestObjectResult(
                new ApiResponse<object>(400, "Length is only available for Redis and must be compared as a number", null)
            );
        }

        if (!AlertMonitorEvaluator.IsOperatorAllowed(request.ValueType, request.Operator)) {
            return new BadRequestObjectResult(new ApiResponse<object>(400, "Operator does not apply to the value type", null));
        }

        return AlertMonitorEvaluator.IsThresholdValid(request.ValueType, request.Threshold.Trim())
            ? null
            : new BadRequestObjectResult(new ApiResponse<object>(400, "Threshold is invalid", null));
    }

    /// <summary>
    ///     保存告警监控，校验来源、指标、类型、操作符和阈值的组合；修改了判定相关配置时清除报警状态，让新配置重新判定
    /// </summary>
    /// <param name="id">监控主键，新增时为空</param>
    /// <param name="request">告警监控保存请求</param>
    /// <returns>保存后的告警监控</returns>
    private async Task<ActionResult<ApiResponse<AlertMonitorListItem>>> SaveAsync(
        long? id
        , SaveAlertMonitorRequest request
    ) {
        var invalid = Validate(request);
        if (invalid is not null) {
            return invalid;
        }

        var row = id.HasValue
            ? await db.AlertMonitors.FindAsync(id.Value).ConfigureAwait(false)
            : new AlertMonitor { Name = request.Name.Trim(), Target = request.Target.Trim() };
        if (row is null) {
            return NotFound(new ApiResponse<object>(404, "Alert monitor does not exist", null));
        }

        var changed = id.HasValue && IsJudgementChanged(row, request);
        row.Name = request.Name.Trim();
        row.Source = request.Source;
        row.CategoryCode = request.Source == AlertMonitorSource.Dictionary ? request.CategoryCode.Trim() : string.Empty;
        row.Target = request.Target.Trim();
        row.Metric = request.Metric;
        row.ValueType = request.ValueType;
        row.Operator = request.Operator;
        row.Threshold = request.Threshold.Trim();
        row.CaseSensitive = request.CaseSensitive;
        row.Mentions = ScheduledJobAlertService.NormalizeMentions(request.Mentions);
        row.Remark = request.Remark.Trim();
        row.IsEnabled = request.IsEnabled;
        if (changed || !row.IsEnabled) {
            row.AlertActive = false;
        }

        if (!id.HasValue) {
            _ = await db.AlertMonitors.AddAsync(row).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<AlertMonitorListItem>.Ok(ToListItem(row), id.HasValue ? "Alert monitor updated" : "Alert monitor created"));
    }
}