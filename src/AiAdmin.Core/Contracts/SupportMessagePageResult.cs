namespace AiAdmin.Api.Contracts;

/// <summary>
///     会话消息分页结果，按时间正序返回当前页，向上翻页时加载更早的消息
/// </summary>
/// <param name="Items">本页消息，按发送时间升序</param>
/// <param name="HasMore">是否还有更早的消息</param>
public sealed record SupportMessagePageResult(IReadOnlyList<SupportMessageItem> Items, bool HasMore);