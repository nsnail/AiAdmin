namespace AiAdmin.Api.Contracts;

/// <summary>
///     发送系统消息请求
/// </summary>
/// <param name="Title">消息标题</param>
/// <param name="Content">富文本消息正文</param>
/// <param name="TargetType">发送对象类型</param>
/// <param name="DepartmentIds">目标部门主键</param>
/// <param name="UserIds">目标用户主键</param>
/// <param name="IsPopup">是否自动弹出提醒</param>
public sealed record SendSystemMessageRequest(string Title, string Content, string TargetType, long[] DepartmentIds, long[] UserIds, bool IsPopup);