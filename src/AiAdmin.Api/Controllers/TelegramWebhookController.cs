using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     Telegram Webhook 控制器
/// </summary>
/// <param name="db">数据库上下文</param>
/// <param name="http">外部请求服务</param>
/// <param name="snapshots">字典快照服务</param>
/// <param name="cache">分布式缓存，缓存机器人用户名</param>
[ApiController]
[AllowAnonymous]
[ApiDescription("Telegram webhook")]
[Route("api/system/telegram-webhook")]
public sealed class TelegramWebhookController(
    AppDbContext db
    , ExternalHttpRequestService http
    , DictionarySnapshotService snapshots
    , IDistributedCache cache) : ControllerBase
{
    /// <summary>
    ///     机器人用户名在 Redis 中的缓存键
    /// </summary>
    private const string _BOT_USERNAME_CACHE_KEY = "telegram:webhook:bot-username";

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

        var token = (await snapshots.GetItemsAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE, ct).ConfigureAwait(false))
            .FirstOrDefault(x => x is { Label: "Telegram Bot Token", IsEnabled: true })
            ?.Value;
        if (string.IsNullOrWhiteSpace(token)) {
            return Ok();
        }

        // 群里只响应 @ 了机器人的消息，私聊不需要 @；取不到机器人用户名时无法判断是否被 @，一律不响应
        var botUsername = await GetBotUsernameAsync(token, ct).ConfigureAwait(false);
        if (botUsername is null || !TelegramBotMention.TryGetCommand(update.Message, botUsername, out var command)) {
            return Ok();
        }

        var cat = await db.DictionaryCategories.Include(x => x.Items).SingleOrDefaultAsync(x => x.Code == "robot_commands", ct).ConfigureAwait(false);
        if (cat is null) {
            return Ok();
        }

        var item = cat.Items.FirstOrDefault(x => x.IsEnabled && x.Label.Equals(command, StringComparison.OrdinalIgnoreCase));
        var text = item is null
            ? string.Join("\n", cat.Items.Where(x => x.IsEnabled).Select(x => $"{x.Label} - {x.Remark}"))
            : await ExecuteScalarAsync(item.Value, ct).ConfigureAwait(false) ?? string.Empty;

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

    /// <summary>
    ///     读取机器人用户名，优先读 Redis 缓存，缺失时调用 Telegram getMe 并缓存一天，失败时返回空
    /// </summary>
    /// <param name="token">机器人令牌</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>机器人用户名，不带 @，获取失败时为空</returns>
    private async Task<string?> GetBotUsernameAsync(
        string token
        , CancellationToken ct
    ) {
        var key = _BOT_USERNAME_CACHE_KEY + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..16];
        var cached = await cache.GetStringAsync(key, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(cached)) {
            return cached;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.telegram.org/bot{token}/getMe");
        var response = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        string? username = null;
        try {
            using var doc = JsonDocument.Parse(response.ResponseBody);
            if (doc.RootElement.TryGetProperty("result", out var result) && result.TryGetProperty("username", out var name)) {
                username = name.GetString();
            }
        }
        catch (JsonException) {
            // 响应不是合法 JSON 时按获取失败处理
        }

        if (string.IsNullOrWhiteSpace(username)) {
            return null;
        }

        await cache
            .SetStringAsync(key, username, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) }, ct)
            .ConfigureAwait(false);
        return username;
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
/// <param name="Type">会话类型，private、group、supergroup 或 channel</param>
public sealed record TelegramChat(
    [property: JsonRequired]
    long Id
    , string? Type = null);