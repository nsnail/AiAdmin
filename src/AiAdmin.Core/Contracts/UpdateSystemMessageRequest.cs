namespace AiAdmin.Api.Contracts;

/// <summary>
///     修改系统消息请求
/// </summary>
/// <param name="Id">消息主键</param>
/// <param name="Title">消息标题</param>
/// <param name="Content">富文本消息正文</param>
public sealed record UpdateSystemMessageRequest(long Id, string Title, string Content);