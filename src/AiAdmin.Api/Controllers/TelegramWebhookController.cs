using System.Globalization;
using System.Text.Json.Serialization;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     Telegram Webhook 控制器
/// </summary>
/// <param name="db">数据库上下文</param>
/// <param name="http">外部请求服务</param>
/// <param name="snapshots">字典快照服务</param>
[ApiController]
[AllowAnonymous]
[ApiDescription("Telegram webhook")]
[Route("api/system/telegram-webhook")]
public sealed class TelegramWebhookController(AppDbContext db, ExternalHttpRequestService http, DictionarySnapshotService snapshots) : ControllerBase
{
    /// <summary>
    ///     处理 Telegram 更新
    /// </summary>
    /// <param name="update">更新内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>处理结果</returns>
    [HttpPost]
    [ApiDescription("Handle Telegram webhook")]
    public async Task<IActionResult> HandleAsync(
        TelegramUpdate update
        , CancellationToken ct
    ) {
        if (update.Message?.Chat is null || string.IsNullOrWhiteSpace(update.Message.Text)) {
            return Ok();
        }

        var cat = await db.DictionaryCategories.Include(x => x.Items).SingleOrDefaultAsync(x => x.Code == "robot_commands", ct).ConfigureAwait(false);
        if (cat is null) {
            return Ok();
        }

        var item = cat.Items.FirstOrDefault(x => x.IsEnabled && x.Label.Equals(update.Message.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        var text = item is null
            ? string.Join("\n", cat.Items.Where(x => x.IsEnabled).Select(x => $"{x.Label} - {x.Remark}"))
            : await ExecuteScalarAsync(item.Value, ct).ConfigureAwait(false) ?? string.Empty;
        var token = (await snapshots.GetItemsAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE, ct).ConfigureAwait(false))
            .FirstOrDefault(x => x is { Label: "Telegram Bot Token", IsEnabled: true })
            ?.Value;
        if (string.IsNullOrWhiteSpace(token)) {
            return Ok();
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.telegram.org/bot{token}/sendMessage");
        req.Content = JsonContent.Create(new { chat_id = update.Message.Chat.Id, text });
        _ = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    ///     执行非组合 SQL 并读取最后一个结果集的首个值
    /// </summary>
    /// <param name="sql">待执行 SQL</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>查询结果文本</returns>
    private async Task<string?> ExecuteScalarAsync(
        string sql
        , CancellationToken ct
    ) {
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        finally {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
///     Telegram 更新
/// </summary>
/// <param name="Message">消息</param>
public sealed record TelegramUpdate(TelegramMessage? Message);

/// <summary>
///     Telegram 消息
/// </summary>
/// <param name="Chat">会话</param>
/// <param name="Text">文本</param>
public sealed record TelegramMessage(TelegramChat? Chat, string? Text);

/// <summary>
///     Telegram 会话
/// </summary>
/// <param name="Id">会话标识</param>
public sealed record TelegramChat(
    [property: JsonRequired]
    long Id);