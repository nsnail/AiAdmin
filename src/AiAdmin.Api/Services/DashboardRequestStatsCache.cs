using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using Microsoft.Extensions.Caching.Distributed;

namespace AiAdmin.Api.Services;

/// <summary>
///     仪表盘操作日志统计的 Redis 缓存：统计需要扫描今天和昨天两天的日分表，日志量大时耗时明显，
///     缓存永不过期，只由计划作业刷新，页面只读缓存，缓存缺失时返回空结果而不会当场扫表
/// </summary>
/// <param name="cache">分布式缓存</param>
/// <param name="scopeFactory">服务作用域工厂，计算时在独立作用域里取统计服务，不依赖当前请求</param>
public sealed class DashboardRequestStatsCache(IDistributedCache cache, IServiceScopeFactory scopeFactory)
{
    /// <summary>
    ///     预热使用的时区偏移分钟数，固定为 UTC+8，与服务器时区一致
    /// </summary>
    private const int _WARM_OFFSET_MINUTES = 480;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     读取计划作业写入的缓存，缓存缺失时返回空结果，不会当场计算；时区固定使用预热的 UTC+8
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作日志统计结果，缓存缺失时为全零的空结果</returns>
    public async Task<DashboardRequestStatsResult> GetAsync(CancellationToken cancellationToken) {
        var cached = await ReadAsync(_WARM_OFFSET_MINUTES, cancellationToken).ConfigureAwait(false);
        return cached ?? new DashboardRequestStatsResult([], [], 0, 0, DateTimeOffset.MinValue);
    }

    /// <summary>
    ///     刷新预热组合（UTC+8）的缓存，供计划作业调用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次写入的缓存条数</returns>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DashboardRequestStatsService>();
        var result = await service.BuildAsync(_WARM_OFFSET_MINUTES, cancellationToken).ConfigureAwait(false);
        await cache.SetStringAsync(CacheKey(_WARM_OFFSET_MINUTES), JsonSerializer.Serialize(result, _json), cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <summary>
    ///     生成缓存键
    /// </summary>
    /// <param name="offsetMinutes">时区偏移分钟数</param>
    /// <returns>缓存键，不含实例前缀</returns>
    private static string CacheKey(int offsetMinutes) {
        return string.Create(CultureInfo.InvariantCulture, $"dashboard-request-stats:{offsetMinutes}");
    }

    /// <summary>
    ///     读取缓存并反序列化，缓存不存在或内容损坏时返回空
    /// </summary>
    /// <param name="offsetMinutes">时区偏移分钟数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存中的统计结果，未命中时为空</returns>
    private async Task<DashboardRequestStatsResult?> ReadAsync(
        int offsetMinutes
        , CancellationToken cancellationToken
    ) {
        var text = await cache.GetStringAsync(CacheKey(offsetMinutes), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text)) {
            return null;
        }

        try {
            return JsonSerializer.Deserialize<DashboardRequestStatsResult>(text, _json);
        }
        catch (JsonException) {
            return null;
        }
    }
}