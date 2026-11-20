using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using Microsoft.Extensions.Caching.Distributed;

namespace AiAdmin.Api.Services;

/// <summary>
///     仪表盘总览的 Redis 缓存：总览包含需要扫描今天和昨天两天日分表的操作日志统计，日志量大时耗时明显，
///     缓存永不过期，只由计划作业按全量数据刷新，页面只读缓存，缓存缺失时返回空结果而不会当场计算
/// </summary>
/// <param name="cache">分布式缓存</param>
/// <param name="scopeFactory">服务作用域工厂，计算时在独立作用域里取统计服务，不依赖当前请求</param>
public sealed class DashboardOverviewCache(IDistributedCache cache, IServiceScopeFactory scopeFactory)
{
    /// <summary>
    ///     预热使用的时区偏移分钟数，固定为 UTC+8，与服务器时区一致
    /// </summary>
    private const int _WARM_OFFSET_MINUTES = 480;

    /// <summary>
    ///     预热的统计天数，与页面可选天数一致
    /// </summary>
    #pragma warning disable SA1202
    public static readonly IReadOnlyList<int> WarmDays = [7, 14, 30];
    #pragma warning restore SA1202

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     读取计划作业写入的缓存，缓存缺失时返回空结果，不会当场计算；时区固定使用预热的 UTC+8
    /// </summary>
    /// <param name="days">统计天数，范围 1 到 90，超出会被限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仪表盘总览结果，缓存缺失时为全零的空结果</returns>
    public async Task<DashboardOverviewResult> GetAsync(
        int days
        , CancellationToken cancellationToken
    ) {
        var cached = await ReadAsync(Math.Clamp(days, 1, 90), cancellationToken).ConfigureAwait(false);
        return cached ?? EmptyResult();
    }

    /// <summary>
    ///     刷新所有预热组合（7、14、30 天，UTC+8）的缓存，供计划作业调用；操作日志统计只扫描一次，挂到每份结果上
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次写入的缓存条数</returns>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var requestStats = await scope
            .ServiceProvider.GetRequiredService<DashboardRequestStatsService>()
            .BuildAsync(_WARM_OFFSET_MINUTES, cancellationToken)
            .ConfigureAwait(false);
        var service = new DashboardOverviewService(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        foreach (var days in WarmDays) {
            var overview = await service.BuildAsync(days, _WARM_OFFSET_MINUTES).ConfigureAwait(false);
            await cache
                .SetStringAsync(CacheKey(days), JsonSerializer.Serialize(overview with { RequestStats = requestStats }, _json), cancellationToken)
                .ConfigureAwait(false);
        }

        return WarmDays.Count;
    }

    /// <summary>
    ///     生成缓存键
    /// </summary>
    /// <param name="days">统计天数</param>
    /// <returns>缓存键，不含实例前缀</returns>
    private static string CacheKey(int days) {
        return string.Create(CultureInfo.InvariantCulture, $"dashboard-overview:{days}:{_WARM_OFFSET_MINUTES}");
    }

    /// <summary>
    ///     缓存缺失时返回的空结果，所有数值为零、列表为空
    /// </summary>
    /// <returns>空的仪表盘总览结果</returns>
    private static DashboardOverviewResult EmptyResult() {
        return new DashboardOverviewResult(
            new DashboardSummaryResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), [], [], [], [], [], [], [], [], []
            , DashboardRequestStatsResult.Empty, DateTimeOffset.MinValue
        );
    }

    /// <summary>
    ///     读取缓存并反序列化，缓存不存在或内容损坏时返回空
    /// </summary>
    /// <param name="days">统计天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存中的总览结果，未命中时为空</returns>
    private async Task<DashboardOverviewResult?> ReadAsync(
        int days
        , CancellationToken cancellationToken
    ) {
        var text = await cache.GetStringAsync(CacheKey(days), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text)) {
            return null;
        }

        try {
            return JsonSerializer.Deserialize<DashboardOverviewResult>(text, _json);
        }
        catch (JsonException) {
            return null;
        }
    }
}