using System.Globalization;
using System.Text;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AiAdmin.Api.Services;

/// <summary>
///     告警监控扫描服务：逐个读取已启用监控的当前值或长度，命中条件时触发报警，恢复后解除报警，通知通过系统设置中的报警机器人发送；
///     由计划作业定期调用，自身不启动后台任务，沿用计划作业对安全停机闸门的登记
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="connectionMultiplexer">Redis 连接复用器</param>
/// <param name="alerts">计划作业报警服务，复用其报警机器人配置读取和 Telegram 发送</param>
public sealed class AlertMonitorScanService(AppDbContext db, IConnectionMultiplexer connectionMultiplexer, ScheduledJobAlertService alerts)
{
    private const int _MAX_MESSAGE_VALUE_LENGTH = 200;

    private const int _MAX_VALUE_LENGTH = 1000;

    /// <summary>
    ///     把判定结果应用到监控：更新最近一次取值和状态，按状态变化决定是否通知
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="outcome">本次判定结果</param>
    /// <param name="now">当前 UTC 时间</param>
    /// <returns>需要发送的通知文本，状态没有变化时为 null</returns>
    public static string? ApplyOutcome(
        AlertMonitor monitor
        , AlertMonitorOutcome outcome
        , DateTime now
    ) {
        monitor.LastValue = Truncate(outcome.Value, _MAX_VALUE_LENGTH);
        monitor.LastMessage = outcome.Message;
        monitor.LastCheckedAt = now;
        switch (outcome.Breached) {
            case true when !monitor.AlertActive:
                monitor.AlertActive = true;
                monitor.AlertedAt = now;
                return BuildText(monitor, outcome, true, now);
            case false when monitor.AlertActive:
                monitor.AlertActive = false;
                return BuildText(monitor, outcome, false, now);
            default:
                return null;
        }
    }

    /// <summary>
    ///     读取 Redis 键的长度：字符串为字节数，List、Set、Hash、SortedSet、Stream 为元素个数；键不存在长度为 0，这是长度监控的常见场景（队列被清空）
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <returns>长度，键不存在或类型不支持时为 0</returns>
    public static async Task<long> ReadRedisLengthAsync(
        IDatabase database
        , string key
    ) {
        var type = await database.KeyTypeAsync(key).ConfigureAwait(false);
        return type switch
        {
            RedisType.String => await database.StringLengthAsync(key).ConfigureAwait(false)
            , RedisType.List => await database.ListLengthAsync(key).ConfigureAwait(false)
            , RedisType.Set => await database.SetLengthAsync(key).ConfigureAwait(false)
            , RedisType.Hash => await database.HashLengthAsync(key).ConfigureAwait(false)
            , RedisType.SortedSet => await database.SortedSetLengthAsync(key).ConfigureAwait(false)
            , RedisType.Stream => await database.StreamLengthAsync(key).ConfigureAwait(false)
            , _ => 0
        };
    }

    /// <summary>
    ///     读取 Redis 字符串键，键不存在或不是字符串类型视为取不到值
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="key">缓存键</param>
    /// <returns>值和取不到值的原因</returns>
    public static async Task<(string? Value, string Reason)> ReadRedisValueAsync(
        IDatabase database
        , string key
    ) {
        var type = await database.KeyTypeAsync(key).ConfigureAwait(false);
        if (type == RedisType.None) {
            return (null, "Redis key does not exist");
        }

        if (type != RedisType.String) {
            return (null, "Redis key is not a string");
        }

        var value = await database.StringGetAsync(key).ConfigureAwait(false);
        return value.HasValue ? (value.ToString(), string.Empty) : (null, "Redis key does not exist");
    }

    /// <summary>
    ///     扫描全部已启用的监控
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次扫描的统计结果</returns>
    public async Task<AlertMonitorScanResult> ScanAsync(CancellationToken cancellationToken) {
        var monitors = await db.AlertMonitors.Where(x => x.IsEnabled).OrderBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var raised = 0;
        var resolved = 0;
        var database = connectionMultiplexer.GetDatabase();
        foreach (var monitor in monitors) {
            var outcome = await EvaluateOneAsync(monitor, database, cancellationToken).ConfigureAwait(false);
            var text = ApplyOutcome(monitor, outcome, DateTime.UtcNow);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 状态先保存再通知：通知失败只记录日志，下次扫描不会重复报警，避免保存失败导致重复轰炸
            if (text is null) {
                continue;
            }

            if (monitor.AlertActive) {
                raised++;
            }
            else {
                resolved++;
            }

            await alerts.NotifyAsync(text, cancellationToken).ConfigureAwait(false);
        }

        return new AlertMonitorScanResult(monitors.Count, raised, resolved, monitors.Count(x => x.AlertActive));
    }

    /// <summary>
    ///     生成报警或解除通知文本
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="outcome">本次判定结果</param>
    /// <param name="raised">true 为报警，false 为解除</param>
    /// <param name="now">当前 UTC 时间</param>
    /// <returns>通知文本</returns>
    private static string BuildText(
        AlertMonitor monitor
        , AlertMonitorOutcome outcome
        , bool raised
        , DateTime now
    ) {
        var builder = new StringBuilder();
        _ = builder.Append(raised ? "\U0001F525 ALERT" : "✅ RESOLVED").Append(": monitor \"").Append(monitor.Name).Append('"');
        _ = builder.Append("\nTarget: ").Append(monitor.Source).Append(' ');
        if (monitor is { Source: AlertMonitorSource.Dictionary, CategoryCode.Length: > 0 }) {
            _ = builder.Append(monitor.CategoryCode).Append('/');
        }

        _ = builder.Append(monitor.Target);
        _ = builder.Append("\nCondition: ").Append(monitor.Metric).Append(' ').Append(monitor.Operator).Append(' ');
        _ = builder.Append(Truncate(monitor.Threshold, _MAX_MESSAGE_VALUE_LENGTH));
        _ = builder.Append('\n').Append(monitor.Metric == AlertMonitorMetric.Length ? "Length: " : "Value: ");
        _ = builder.Append(outcome.Value.Length == 0 ? "-" : Truncate(outcome.Value, _MAX_MESSAGE_VALUE_LENGTH));
        if (raised) {
            _ = builder.Append("\nReason: ").Append(outcome.Message);
        }

        _ = builder.Append("\nTime (UTC): ").Append(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if (raised && !string.IsNullOrWhiteSpace(monitor.Mentions)) {
            _ = builder.Append('\n').Append(monitor.Mentions);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     截断过长文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="max">最大长度</param>
    /// <returns>截断后的文本</returns>
    private static string Truncate(
        string text
        , int max
    ) {
        return text.Length > max ? text[..max] : text;
    }

    /// <summary>
    ///     读取单个监控的当前值或长度并判定，读取过程出错视为取不到值
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="database">Redis 数据库</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>判定结果</returns>
    private async Task<AlertMonitorOutcome> EvaluateOneAsync(
        AlertMonitor monitor
        , IDatabase database
        , CancellationToken cancellationToken
    ) {
        try {
            if (monitor is { Source: AlertMonitorSource.Redis, Metric: AlertMonitorMetric.Length }) {
                var length = await ReadRedisLengthAsync(database, monitor.Target).ConfigureAwait(false);
                return AlertMonitorEvaluator.Evaluate(monitor, length.ToString(CultureInfo.InvariantCulture), string.Empty);
            }

            var (value, reason) = monitor.Source == AlertMonitorSource.Redis
                ? await ReadRedisValueAsync(database, monitor.Target).ConfigureAwait(false)
                : await ReadDictionaryAsync(monitor, cancellationToken).ConfigureAwait(false);
            return AlertMonitorEvaluator.Evaluate(monitor, value, reason);
        }
        catch (Exception exception) when (exception is RedisException or InvalidOperationException) {
            return new AlertMonitorOutcome(true, string.Empty, "Value could not be read");
        }
    }

    /// <summary>
    ///     读取字典内容，目录或标签不存在、或字典项已停用视为取不到值
    /// </summary>
    /// <param name="monitor">告警监控</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>值和取不到值的原因</returns>
    private async Task<(string? Value, string Reason)> ReadDictionaryAsync(
        AlertMonitor monitor
        , CancellationToken cancellationToken
    ) {
        var code = monitor.CategoryCode;
        var label = monitor.Target;
        var item = await db
            .DictionaryItems.AsNoTracking()
            .Where(x => x.Label == label && x.Category.Code == code)
            .Select(x => new { x.Value, x.IsEnabled })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (item is null) {
            return (null, "Dictionary item does not exist");
        }

        return item.IsEnabled ? (item.Value, string.Empty) : (null, "Dictionary item is disabled");
    }
}