using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     仪表盘统计控制器，为大屏提供用户、登录、计划作业等运营数据
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="systemInfo">系统信息服务</param>
/// <param name="overviewService">总览统计服务</param>
/// <param name="overviewCache">总览缓存</param>
[ApiController]
[Route("api/dashboard")]
[ApiDescription("Dashboard statistics")]
public sealed class DashboardController(
    AppDbContext db
    , SystemInfoService systemInfo
    , DashboardOverviewService overviewService
    , DashboardOverviewCache overviewCache) : ControllerBase
{
    /// <summary>
    ///     查询仪表盘总览数据，包含用户、登录、计划作业等运营数据以及操作日志统计
    ///     拥有全部数据权限的用户只读计划作业刷新的缓存，缓存缺失时返回空结果；其他用户实时统计运营数据，操作日志统计为空，不使用缓存
    /// </summary>
    /// <param name="days">统计天数，范围 1 到 90</param>
    /// <param name="offsetMinutes">客户端相对 UTC 的时区偏移分钟数，用于按本地日期分组，读取缓存时固定使用预热的 UTC+8</param>
    /// <returns>仪表盘总览结果</returns>
    [HttpGet("overview")]
    [ApiDescription("Query dashboard overview")]
    public async Task<ActionResult<ApiResponse<DashboardOverviewResult>>> OverviewAsync(
        [FromQuery] int days = 14
        , [FromQuery] int offsetMinutes = 0
    ) {
        var result = db.DataScope.HasAllData
            ? await overviewCache.GetAsync(days, HttpContext.RequestAborted).ConfigureAwait(false)
            : await overviewService.BuildAsync(days, offsetMinutes).ConfigureAwait(false);
        return Ok(ApiResponse<DashboardOverviewResult>.Ok(result));
    }

    /// <summary>
    ///     刷新仪表盘总览缓存，供计划作业调用：按页面可选的 7、14、30 天和固定的 UTC+8 时区重新计算并写入缓存，操作日志统计只扫描一次
    ///     统计按全量数据计算，因此只允许全部数据权限调用
    /// </summary>
    /// <returns>本次刷新的缓存条数</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpPost("overview/refresh")]
    [ApiDescription("Refresh dashboard overview cache")]
    public async Task<ActionResult<ApiResponse<RefreshDashboardOverviewResult>>> RefreshOverviewAsync() {
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        var refreshed = await overviewCache.RefreshAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(ApiResponse<RefreshDashboardOverviewResult>.Ok(new RefreshDashboardOverviewResult(refreshed)));
    }

    /// <summary>
    ///     查询系统信息，名称和版本号读取自当前程序集元数据，更新内容读取自嵌入的 CHANGELOG
    /// </summary>
    /// <returns>系统名称、版本号与最近一次版本的更新内容</returns>
    [HttpGet("system-info")]
    [ApiDescription("Query dashboard system info")]
    public ActionResult<ApiResponse<DashboardSystemInfoResult>> SystemInfo() {
        return Ok(ApiResponse<DashboardSystemInfoResult>.Ok(systemInfo.Info));
    }
}