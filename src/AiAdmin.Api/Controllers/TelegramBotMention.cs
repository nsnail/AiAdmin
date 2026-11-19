namespace AiAdmin.Api.Controllers;

/// <summary>
///     解析群消息中对机器人的 @ 提及，并去掉提及后返回纯指令文本
/// </summary>
public static class TelegramBotMention
{
    /// <summary>
    ///     判断消息是否应由机器人处理，私聊始终处理，群组、超级群和频道必须 @ 了机器人，并返回去掉提及后的指令文本
    /// </summary>
    /// <param name="message">Telegram 消息</param>
    /// <param name="botUsername">机器人用户名，不带 @</param>
    /// <param name="command">去掉 @ 机器人后的指令文本，不应处理时为空</param>
    /// <returns>消息是否应被处理</returns>
    public static bool TryGetCommand(
        TelegramMessage message
        , string botUsername
        , out string command
    ) {
        var text = message.Text?.Trim() ?? string.Empty;
        if (string.Equals(message.Chat?.Type, "private", StringComparison.OrdinalIgnoreCase)) {
            command = StripMentions(text, botUsername);
            return true;
        }

        var mention = "@" + botUsername;
        if (botUsername.Length == 0 || text.IndexOf(mention, StringComparison.OrdinalIgnoreCase) < 0) {
            command = string.Empty;
            return false;
        }

        command = StripMentions(text, botUsername);
        return true;
    }

    /// <summary>
    ///     去掉文本中所有对机器人的 @ 提及，同时兼容 /cmd@bot 和 @bot /cmd 两种写法，并压缩多余空白
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="botUsername">机器人用户名，不带 @</param>
    /// <returns>去掉提及后的文本</returns>
    private static string StripMentions(
        string text
        , string botUsername
    ) {
        if (botUsername.Length == 0) {
            return text;
        }

        var stripped = text.Replace("@" + botUsername, string.Empty, StringComparison.OrdinalIgnoreCase);
        return string.Join(' ', stripped.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}