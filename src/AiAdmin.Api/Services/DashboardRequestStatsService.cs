using System.Globalization;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     仪表盘操作日志统计服务：扫描今天和昨天的日分表，统计按小时的访问量对比和今日各接口的请求占比，
///     供统计接口缓存未命中时计算，以及计划作业刷新缓存时共用，保证两条路径的统计口径一致
/// </summary>
/// <param name="db">应用数据库上下文，用于还原接口路径和说明</param>
/// <param name="requestLogs">操作日志分表服务</param>
public sealed class DashboardRequestStatsService(AppDbContext db, ApiRequestLogTableService requestLogs)
{
    private const int _MAX_API_SLICES = 8;

    /// <summary>
    ///     计算操作日志统计：今日与昨日按本地小时的访问量对比，以及今日各接口的请求占比
    /// </summary>
    /// <param name="offsetMinutes">客户端相对 UTC 的时区偏移分钟数，范围 -840 到 840，超出会被限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作日志统计结果</returns>
    public async Task<DashboardRequestStatsResult> BuildAsync(
        int offsetMinutes
        , CancellationToken cancellationToken
    ) {
        offsetMinutes = Math.Clamp(offsetMinutes, -840, 840);
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var now = ServerTime.UtcNow;
        var todayStart = (now + offset).Date - offset;
        var yesterdayStart = todayStart.AddDays(-1);
        var tomorrowStart = todayStart.AddDays(1);
        var byHour = await requestLogs.CountByHourAsync(yesterdayStart, tomorrowStart, cancellationToken).ConfigureAwait(false);
        var byApi = await requestLogs.CountByApiAsync(todayStart, tomorrowStart, cancellationToken).ConfigureAwait(false);

        // 小时对齐到本地：把 UTC 整点换成本地小时，今日尚未到达的小时记 0
        var trend = Enumerable
            .Range(0, 24)
            .Select(hour => new DashboardTrendPoint(
                    hour.ToString("00", CultureInfo.InvariantCulture), byHour.GetValueOrDefault(todayStart.AddHours(hour))
                    , byHour.GetValueOrDefault(yesterdayStart.AddHours(hour))
                )
            )
            .ToList();
        var distribution = await BuildApiDistributionAsync(byApi, cancellationToken).ConfigureAwait(false);
        return new DashboardRequestStatsResult(trend, distribution, trend.Sum(x => x.Primary), trend.Sum(x => x.Secondary), ServerTime.ToOffset(now));
    }

    /// <summary>
    ///     取请求数最多的若干个接口，其余合并为一项，名称使用接口路径，接口表里找不到时使用路径 CRC32 的数值
    /// </summary>
    /// <param name="byApi">以路径 CRC32 为键的请求数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按请求数倒序的接口分布，路径和说明都为空表示合并的其他项</returns>
    private async Task<IReadOnlyList<DashboardApiShare>> BuildApiDistributionAsync(
        Dictionary<int, int> byApi
        , CancellationToken cancellationToken
    ) {
        var top = byApi.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(_MAX_API_SLICES).ToList();
        List<int> crcs = [.. top.Select(x => x.Key)];
        var paths = await db
            .ApiEndpoints.AsNoTracking()
            .Where(x => crcs.Contains(x.PathCrc32))
            .Select(x => new { x.PathCrc32, x.Path, x.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var apiByCrc = new Dictionary<int, (string Path, string Name)>();
        foreach (var item in paths) {
            _ = apiByCrc.TryAdd(item.PathCrc32, (item.Path, item.Name));
        }

        var result = top.ConvertAll(x => apiByCrc.TryGetValue(x.Key, out var api)
            ? new DashboardApiShare(api.Path, api.Name, x.Value, x.Key)
            : new DashboardApiShare(x.Key.ToString(CultureInfo.InvariantCulture), string.Empty, x.Value, x.Key)
        );
        var others = byApi.Values.Sum() - top.Sum(x => x.Value);
        if (others > 0) {
            result.Add(new DashboardApiShare(string.Empty, string.Empty, others));
        }

        return result;
    }
}