using System.Globalization;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     仪表盘统计控制器，为大屏提供用户、登录、计划作业等运营数据
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="systemInfo">系统信息服务</param>
/// <param name="requestLogs">操作日志分表服务</param>
[ApiController]
[Route("api/dashboard")]
[ApiDescription("Dashboard statistics")]
public sealed class DashboardController(AppDbContext db, SystemInfoService systemInfo, ApiRequestLogTableService requestLogs) : ControllerBase
{
    private const int _MAX_API_SLICES = 8;

    /// <summary>
    ///     查询仪表盘总览数据
    /// </summary>
    /// <param name="days">统计天数，范围 1 到 90</param>
    /// <param name="offsetMinutes">客户端相对 UTC 的时区偏移分钟数，用于按本地日期分组</param>
    /// <returns>仪表盘总览结果</returns>
    [HttpGet("overview")]
    [ApiDescription("Query dashboard overview")]
    public async Task<ActionResult<ApiResponse<DashboardOverviewResult>>> OverviewAsync(
        [FromQuery] int days = 14
        , [FromQuery] int offsetMinutes = 0
    ) {
        days = Math.Clamp(days, 1, 90);
        offsetMinutes = Math.Clamp(offsetMinutes, -840, 840);
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var now = ServerTime.UtcNow;
        var localToday = (now + offset).Date;
        var rangeStartLocal = localToday.AddDays(-(days - 1));
        var rangeStart = rangeStartLocal - offset;
        var todayStart = localToday - offset;

        var loginRows = await db
            .LoginLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= rangeStart)
            .Select(x => new
                {
                    x.CreatedAt
                    , x.UserId
                    , x.DeviceType
                    , x.Browser
                    , x.Region
                }
            )
            .ToListAsync()
            .ConfigureAwait(false);
        var userDates = await db
            .Users.AsNoTracking()
            .Where(x => x.CreatedAt >= rangeStart)
            .Select(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
        var executionRows = await db
            .ScheduledJobExecutions.AsNoTracking()
            .Where(x => x.StartedAt >= rangeStart)
            .Select(x => new { x.StartedAt, x.Status })
            .ToListAsync()
            .ConfigureAwait(false);

        var localDays = new List<DateTime>(days);
        for (var i = 0; i < days; ++i) {
            localDays.Add(rangeStartLocal.AddDays(i));
        }

        var loginTrend = localDays.ConvertAll(d =>
            {
                var rows = loginRows.Where(x => (x.CreatedAt + offset).Date == d).ToList();
                return new DashboardTrendPoint(FormatDate(d), rows.Count, rows.Select(x => x.UserId).Distinct().Count());
            }
        );
        var newUserTrend = localDays.ConvertAll(d => new DashboardTrendPoint(FormatDate(d), userDates.Count(x => (x + offset).Date == d), 0));
        var executionTrend = localDays.ConvertAll(d =>
            {
                var rows = executionRows.Where(x => (x.StartedAt + offset).Date == d).ToList();
                return new DashboardTrendPoint(
                    FormatDate(d), rows.Count(x => x.Status == ScheduledJobStatus.Success)
                    , rows.Count(x => x.Status is ScheduledJobStatus.Failed or ScheduledJobStatus.Timeout)
                );
            }
        );

        var finishedExecutions
            = executionRows.Count(x => x.Status is ScheduledJobStatus.Success or ScheduledJobStatus.Failed or ScheduledJobStatus.Timeout);
        var successExecutions = executionRows.Count(x => x.Status == ScheduledJobStatus.Success);
        var successRate = finishedExecutions == 0 ? 0 : Math.Round((decimal)successExecutions * 100 / finishedExecutions, 1);

        var todayLogins = loginRows.Where(x => x.CreatedAt >= todayStart).ToList();
        var jobStatusGroups = await db
            .ScheduledJobs.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync()
            .ConfigureAwait(false);
        var totalJobs = jobStatusGroups.Sum(x => x.Count);
        var enabledJobs = await db.ScheduledJobs.AsNoTracking().CountAsync(x => x.IsEnabled).ConfigureAwait(false);
        var totalUsers = await db.Users.AsNoTracking().CountAsync().ConfigureAwait(false);
        var enabledUsers = await db.Users.AsNoTracking().CountAsync(x => x.IsEnabled).ConfigureAwait(false);
        var wallets = await db.Wallets.AsNoTracking().Select(x => new { x.AvailableBalance, x.FrozenBalance }).ToListAsync().ConfigureAwait(false);

        var summary = new DashboardSummaryResult(
            totalUsers, enabledUsers, userDates.Count(x => x >= todayStart), userDates.Count, todayLogins.Count
            , todayLogins.Select(x => x.UserId).Distinct().Count(), totalJobs, enabledJobs
            , jobStatusGroups.Where(x => x.Status == ScheduledJobStatus.Running).Sum(x => x.Count)
            , jobStatusGroups.Where(x => x.Status is ScheduledJobStatus.Failed or ScheduledJobStatus.Timeout).Sum(x => x.Count)
            , executionRows.Count(x => x.StartedAt >= todayStart), successRate, await db.Roles.AsNoTracking().CountAsync().ConfigureAwait(false)
            , await db.Departments.AsNoTracking().CountAsync().ConfigureAwait(false), wallets.Sum(x => x.AvailableBalance)
            , wallets.Sum(x => x.FrozenBalance)
        );

        var recentLogins = await db
            .LoginLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => new
                {
                    x.UserName
                    , x.ClientIp
                    , x.Region
                    , x.Browser
                    , x.CreatedAt
                }
            )
            .ToListAsync()
            .ConfigureAwait(false);
        var recentFailures = await db
            .ScheduledJobExecutions.AsNoTracking()
            .Where(x => x.Status == ScheduledJobStatus.Failed || x.Status == ScheduledJobStatus.Timeout)
            .OrderByDescending(x => x.StartedAt)
            .Take(8)
            .Select(x => new
                {
                    x.Id
                    , x.ScheduledJobId
                    , JobName = x.ScheduledJob.Name
                    , x.Status
                    , x.ErrorMessage
                    , x.StartedAt
                }
            )
            .ToListAsync()
            .ConfigureAwait(false);

        var result = new DashboardOverviewResult(
            summary, loginTrend, newUserTrend, executionTrend, Distribute(loginRows.Select(x => x.DeviceType), 6)
            , Distribute(loginRows.Select(x => x.Browser), 6), Distribute(loginRows.Select(x => x.Region), 8)
            , [.. jobStatusGroups.Select(x => new DashboardNameValue(x.Status.ToString(), x.Count))]
            , recentLogins.ConvertAll(x => new DashboardRecentLoginResult(
                    x.UserName, x.ClientIp, x.Region, x.Browser, ServerTime.ToOffset(x.CreatedAt)
                )
            ), recentFailures.ConvertAll(x => new DashboardFailedExecutionResult(x.Id, x.ScheduledJobId, x.JobName, x.Status, x.ErrorMessage, ServerTime.ToOffset(x.StartedAt)))
            , ServerTime.ToOffset(now)
        );
        return Ok(ApiResponse<DashboardOverviewResult>.Ok(result));
    }

    /// <summary>
    ///     查询操作日志统计：今日与昨日按小时的访问量对比，以及今日各接口的请求占比，只扫描今天和昨天两天的日表
    /// </summary>
    /// <param name="offsetMinutes">客户端相对 UTC 的时区偏移分钟数，用于按本地日期和小时分组</param>
    /// <returns>操作日志统计结果</returns>
    /// <exception cref="DataAccessDeniedException">当前用户没有全部数据权限时引发</exception>
    [HttpGet("request-stats")]
    [ApiDescription("Query dashboard request statistics")]
    public async Task<ActionResult<ApiResponse<DashboardRequestStatsResult>>> RequestStatsAsync([FromQuery] int offsetMinutes = 0) {
        // 操作日志包含全部用户的访问记录，与操作日志页一致，只允许全部数据权限查看
        if (!db.DataScope.HasAllData) {
            throw new DataAccessDeniedException();
        }

        offsetMinutes = Math.Clamp(offsetMinutes, -840, 840);
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var now = ServerTime.UtcNow;
        var todayStart = (now + offset).Date - offset;
        var yesterdayStart = todayStart.AddDays(-1);
        var tomorrowStart = todayStart.AddDays(1);
        var byHour = await requestLogs.CountByHourAsync(yesterdayStart, tomorrowStart, HttpContext.RequestAborted).ConfigureAwait(false);
        var byApi = await requestLogs.CountByApiAsync(todayStart, tomorrowStart, HttpContext.RequestAborted).ConfigureAwait(false);

        // 小时对齐到本地：把 UTC 整点换成本地小时，今日尚未到达的小时记 0
        var trend = Enumerable
            .Range(0, 24)
            .Select(hour => new DashboardTrendPoint(
                    hour.ToString("00", CultureInfo.InvariantCulture), byHour.GetValueOrDefault(todayStart.AddHours(hour))
                    , byHour.GetValueOrDefault(yesterdayStart.AddHours(hour))
                )
            )
            .ToList();
        var distribution = await BuildApiDistributionAsync(byApi).ConfigureAwait(false);
        return Ok(
            ApiResponse<DashboardRequestStatsResult>.Ok(
                new DashboardRequestStatsResult(trend, distribution, trend.Sum(x => x.Primary), trend.Sum(x => x.Secondary), ServerTime.ToOffset(now))
            )
        );
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

    /// <summary>
    ///     按值分组计数并按数量倒序取前若干项，空值归入 Unknown
    /// </summary>
    /// <param name="values">原始值序列</param>
    /// <param name="take">最多返回的项数</param>
    /// <returns>名称与数量列表</returns>
    private static List<DashboardNameValue> Distribute(
        IEnumerable<string> values
        , int take
    ) {
        return
        [
            .. values
                .GroupBy(x => string.IsNullOrWhiteSpace(x) ? "Unknown" : x)
                .Select(g => new DashboardNameValue(g.Key, g.Count()))
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Take(take)
        ];
    }

    /// <summary>
    ///     将日期格式化为 yyyy-MM-dd
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>固定区域性的日期文本</returns>
    private static string FormatDate(DateTime date) {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     取请求数最多的若干个接口，其余合并为一项，名称使用接口路径，接口表里找不到时使用路径 CRC32 的数值
    /// </summary>
    /// <param name="byApi">以路径 CRC32 为键的请求数</param>
    /// <returns>按请求数倒序的接口分布，路径和说明都为空表示合并的其他项</returns>
    private async Task<IReadOnlyList<DashboardApiShare>> BuildApiDistributionAsync(Dictionary<int, int> byApi) {
        var top = byApi.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(_MAX_API_SLICES).ToList();
        List<int> crcs = [.. top.Select(x => x.Key)];
        var paths = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => crcs.Contains(x.PathCrc32))
            .Select(x => new { x.PathCrc32, x.Path, x.Name })
            .ToListAsync(HttpContext.RequestAborted)
            .ConfigureAwait(false);
        var apiByCrc = new Dictionary<int, (string Path, string Name)>();
        foreach (var item in paths) {
            _ = apiByCrc.TryAdd(item.PathCrc32, (item.Path, item.Name));
        }

        var result = top.ConvertAll(x => apiByCrc.TryGetValue(x.Key, out var api)
            ? new DashboardApiShare(api.Path, api.Name, x.Value)
            : new DashboardApiShare(x.Key.ToString(CultureInfo.InvariantCulture), string.Empty, x.Value)
        );
        var others = byApi.Values.Sum() - top.Sum(x => x.Value);
        if (others > 0) {
            result.Add(new DashboardApiShare(string.Empty, string.Empty, others));
        }

        return result;
    }
}