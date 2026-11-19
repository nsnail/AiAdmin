using AiAdmin.Api.Controllers;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     机器人 @ 提及解析测试，覆盖私聊、群里是否 @ 以及两种指令写法
/// </summary>
public sealed class TelegramBotMentionTests
{
    /// <summary>
    ///     验证取不到机器人用户名时群里一律不处理
    /// </summary>
    [Fact]
    public void EmptyBotUsername_IgnoresGroupMessage() {
        Assert.False(TelegramBotMention.TryGetCommand(Message("group", "@ /stats"), string.Empty, out _));
    }

    /// <summary>
    ///     验证群、超级群和频道里没有 @ 机器人时不处理
    /// </summary>
    /// <param name="chatType">会话类型</param>
    [Theory]
    [InlineData("group")]
    [InlineData("supergroup")]
    [InlineData("channel")]
    [InlineData(null)]
    public void GroupChat_IgnoresMessageWithoutMention(string? chatType) {
        Assert.False(TelegramBotMention.TryGetCommand(Message(chatType, "/stats"), "tg_bot", out var command));
        Assert.Equal(string.Empty, command);
        Assert.False(TelegramBotMention.TryGetCommand(Message(chatType, "@other_bot /stats"), "tg_bot", out _));
    }

    /// <summary>
    ///     验证群里两种 @ 写法都会去掉提及并忽略大小写，机器人名只是前缀的其他用户不会误判
    /// </summary>
    /// <param name="text">消息文本</param>
    /// <param name="expected">期望的指令文本</param>
    [Theory]
    [InlineData("@tg_bot /stats", "/stats")]
    [InlineData("/stats@tg_bot", "/stats")]
    [InlineData("  @TG_BOT   /stats  ", "/stats")]
    [InlineData("/stats @tg_bot", "/stats")]
    public void GroupChat_StripsMention(
        string text
        , string expected
    ) {
        Assert.True(TelegramBotMention.TryGetCommand(Message("supergroup", text), "tg_bot", out var command));
        Assert.Equal(expected, command);
    }

    /// <summary>
    ///     验证私聊不需要 @ 也会处理，指令原样返回
    /// </summary>
    [Fact]
    public void PrivateChat_HandlesWithoutMention() {
        var handled = TelegramBotMention.TryGetCommand(Message("private", " /stats "), "tg_bot", out var command);

        Assert.True(handled);
        Assert.Equal("/stats", command);
    }

    /// <summary>
    ///     构造 Telegram 消息
    /// </summary>
    /// <param name="chatType">会话类型</param>
    /// <param name="text">消息文本</param>
    /// <returns>Telegram 消息</returns>
    private static TelegramMessage Message(
        string? chatType
        , string text
    ) {
        return new TelegramMessage(new TelegramChat(1, chatType), text);
    }
}