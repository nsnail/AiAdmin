using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     计划作业报警服务：判断作业是否触发或解除报警，并通过系统设置中的报警机器人发送 Telegram 通知
/// </summary>
/// <param name="snapshots">字典快照服务</param>
/// <param name="http">外部请求服务</param>
/// <param name="logger">日志记录器</param>
public sealed partial class ScheduledJobAlertService(
    DictionarySnapshotService snapshots
    , ExternalHttpRequestService http
    , ILogger<ScheduledJobAlertService> logger)
{
    /// <summary>
    ///     报警机器人令牌在系统设置中的标签
    /// </summary>
    private const string _ALERT_BOT_TOKEN_LABEL = "Telegram Alert Bot Token";

    /// <summary>
    ///     报警机器人会话编号在系统设置中的标签
    /// </summary>
    private const string _ALERT_CHAT_ID_LABEL = "Telegram Alert Chat Id";

    private const int _MAX_ERROR_LENGTH = 500;

    private const int _MAX_MENTIONS = 20;

    /// <summary>
    ///     报警通知的前缀图标：火焰表情 U+1F525
    /// </summary>
    private const string _RAISED_EMOJI = "\U0001F525";

    /// <summary>
    ///     解除通知的前缀图标：对勾表情 U+2705
    /// </summary>
    private const string _RESOLVED_EMOJI = "\u2705";

    private static readonly Action<ILogger, Exception?> _logNotConfigured = LoggerMessage.Define(
        LogLevel.Warning, new EventId(3603, "ScheduledJobAlertNotConfigured"), "Scheduled job alert skipped because the alert bot is not configured"
    );

    private static readonly Action<ILogger, Exception?> _logSendFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(3601, "ScheduledJobAlertSendFailed"), "Scheduled job alert could not be sent"
    );

    private static readonly Action<ILogger, int, Exception?> _logSendRejected = LoggerMessage.Define<int>(
        LogLevel.Warning, new EventId(3602, "ScheduledJobAlertSendRejected"), "Scheduled job alert was rejected with status {StatusCode}"
    );

    private static readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     判断报警动作并更新作业的报警状态：未处于报警时命中条件才触发，处于报警时作业结束且不再命中条件才解除，持续命中期间不重复触发
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="outcome">本次状态</param>
    /// <param name="durationMs">耗时毫秒数</param>
    /// <param name="finished">作业是否已结束</param>
    /// <param name="responseBody">本次完整响应体，请求超时、异常或作业仍在运行时为 null，此时不按执行结果规则判定</param>
    /// <returns>报警动作</returns>
    public static ScheduledJobAlertKind Evaluate(
        ScheduledJob job
        , ScheduledJobStatus outcome
        , long durationMs
        , bool finished
        , string? responseBody = null
    ) {
        if (!job.AlertEnabled) {
            job.AlertActive = false;
            return ScheduledJobAlertKind.None;
        }

        switch (IsBreached(job, outcome, durationMs, responseBody).Breached) {
            case true when !job.AlertActive:
                job.AlertActive = true;
                return ScheduledJobAlertKind.Raised;
            case false when finished && job.AlertActive:
                job.AlertActive = false;
                return ScheduledJobAlertKind.Resolved;
            default:
                return ScheduledJobAlertKind.None;
        }
    }

    /// <summary>
    ///     规范化提醒用户名：按空白、逗号或分号拆分，每项补齐 @ 前缀，去重并限制数量
    /// </summary>
    /// <param name="value">用户填写的原始文本</param>
    /// <returns>以空格分隔的规范化用户名，没有有效项时为空字符串</returns>
    public static string NormalizeMentions(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var names = MentionSeparatorRegex()
            .Split(value)
            .Select(x => x.Trim().TrimStart('@'))
            .Where(x => x.Length > 0)
            .Select(x => $"@{x}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_MAX_MENTIONS);
        return string.Join(' ', names);
    }

    /// <summary>
    ///     判断作业本次状态应触发还是解除报警，并生成通知文本；同时更新作业的报警状态，调用方负责保存
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="outcome">本次状态，运行中的作业传 Running</param>
    /// <param name="durationMs">已用或总耗时毫秒数</param>
    /// <param name="error">错误信息，没有时为空字符串</param>
    /// <param name="finished">作业是否已结束，未结束时只会触发报警，不会解除</param>
    /// <param name="responseBody">本次完整响应体，没有响应体时为 null</param>
    /// <returns>需要发送的通知文本，无需通知时为 null</returns>
    public static string? Prepare(
        ScheduledJob job
        , ScheduledJobStatus outcome
        , long durationMs
        , string error
        , bool finished
        , string? responseBody = null
    ) {
        var kind = Evaluate(job, outcome, durationMs, finished, responseBody);
        if (kind == ScheduledJobAlertKind.None) {
            return null;
        }

        // 触发报警时说明命中的是执行结果规则，便于不翻执行记录就能看到原因
        var resultReason = kind == ScheduledJobAlertKind.Raised ? IsBreached(job, outcome, durationMs, responseBody).ResultReason : string.Empty;
        return BuildText(kind, job, outcome, durationMs, error, resultReason);
    }

    /// <summary>
    ///     使用系统设置中的报警机器人发送通知，未配置令牌或会话编号时跳过；发送失败只记录日志，不影响作业
    /// </summary>
    /// <param name="text">通知文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步发送任务</returns>
    public async Task NotifyAsync(
        string text
        , CancellationToken cancellationToken
    ) {
        var settings = await snapshots.GetItemsAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE, cancellationToken).ConfigureAwait(false);
        var token = settings.FirstOrDefault(x => x is { IsEnabled: true, Label: _ALERT_BOT_TOKEN_LABEL })?.Value;
        var chatId = settings.FirstOrDefault(x => x is { IsEnabled: true, Label: _ALERT_CHAT_ID_LABEL })?.Value;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) {
            _logNotConfigured(logger, null);
            return;
        }

        await SendAsync(token.Trim(), chatId.Trim(), text, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     生成报警或解除通知文本
    /// </summary>
    /// <param name="kind">报警动作</param>
    /// <param name="job">计划作业</param>
    /// <param name="outcome">本次状态</param>
    /// <param name="durationMs">耗时毫秒数</param>
    /// <param name="error">错误信息</param>
    /// <param name="resultReason">命中执行结果规则时的说明，没有命中时为空字符串</param>
    /// <returns>通知文本</returns>
    private static string BuildText(
        ScheduledJobAlertKind kind
        , ScheduledJob job
        , ScheduledJobStatus outcome
        , long durationMs
        , string error
        , string resultReason
    ) {
        var builder = new StringBuilder();
        _ = builder
            .Append(kind == ScheduledJobAlertKind.Raised ? _RAISED_EMOJI + " ALERT" : _RESOLVED_EMOJI + " RESOLVED")
            .Append(": scheduled job \"")
            .Append(job.Name)
            .Append('"');
        _ = builder.Append("\nStatus: ").Append(outcome);
        _ = builder.Append("\nDuration: ").Append((durationMs / 1000d).ToString("0.#", CultureInfo.InvariantCulture)).Append('s');
        if (kind == ScheduledJobAlertKind.Raised && !string.IsNullOrWhiteSpace(error)) {
            _ = builder.Append("\nError: ").Append(error.Length > _MAX_ERROR_LENGTH ? error[.._MAX_ERROR_LENGTH] : error);
        }

        if (kind == ScheduledJobAlertKind.Raised && resultReason.Length > 0) {
            _ = builder.Append("\nResult: ").Append(resultReason.Length > _MAX_ERROR_LENGTH ? resultReason[.._MAX_ERROR_LENGTH] : resultReason);
        }

        _ = builder.Append("\nTime (UTC): ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(job.AlertMentions)) {
            _ = builder.Append('\n').Append(job.AlertMentions);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     判断是否命中报警条件：失败或超时，或耗时超过设定秒数，或完整响应体命中执行结果规则
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="outcome">本次状态</param>
    /// <param name="durationMs">耗时毫秒数</param>
    /// <param name="responseBody">本次完整响应体，没有响应体时为 null，此时不按执行结果规则判定</param>
    /// <returns>是否命中，以及命中执行结果规则时的说明</returns>
    private static (bool Breached, string ResultReason) IsBreached(
        ScheduledJob job
        , ScheduledJobStatus outcome
        , long durationMs
        , string? responseBody
    ) {
        var failed = job.AlertOnFailure && outcome is ScheduledJobStatus.Failed or ScheduledJobStatus.Timeout;
        var tooLong = job.AlertOverSeconds > 0 && durationMs > job.AlertOverSeconds * 1000L;
        var resultReason = MatchResult(job, responseBody);
        return (failed || tooLong || resultReason.Length > 0, resultReason);
    }

    /// <summary>
    ///     按作业的执行结果规则匹配完整响应体：没有配置规则或没有响应体时不命中；响应体无法按规则处理（如不是数字、正则非法或超时）时视为命中并说明原因，避免漏报
    /// </summary>
    /// <param name="job">计划作业</param>
    /// <param name="responseBody">完整响应体，没有响应体时为 null</param>
    /// <returns>命中时的说明，没有命中时为空字符串</returns>
    private static string MatchResult(
        ScheduledJob job
        , string? responseBody
    ) {
        if (job.AlertResultOperator is not { } op || responseBody is null) {
            return string.Empty;
        }

        var result = ResultMatcher.Match(job.AlertResultValueType, op, responseBody, job.AlertResultValue, job.AlertResultCaseSensitive);
        if (result.Failed) {
            return result.Reason;
        }

        return result.Hit ? responseBody.Trim() : string.Empty;
    }

    [GeneratedRegex(@"[\s,;]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MentionSeparatorRegex();

    /// <summary>
    ///     调用 Telegram sendMessage 发送纯文本消息
    /// </summary>
    /// <param name="token">机器人令牌</param>
    /// <param name="chatId">会话编号，群组和频道为负数</param>
    /// <param name="text">消息文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步发送任务</returns>
    private async Task SendAsync(
        string token
        , string chatId
        , string text
        , CancellationToken cancellationToken
    ) {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.telegram.org/bot{token}/sendMessage");
            request.Content = JsonContent.Create(new { chat_id = chatId, text });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_sendTimeout);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300) {
                _logSendRejected(logger, response.StatusCode, null);
            }
        }
        catch (Exception exception) {
            _logSendFailed(logger, exception);
        }
    }
}