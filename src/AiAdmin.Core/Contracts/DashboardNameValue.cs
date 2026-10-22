namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘名称与数量键值
/// </summary>
/// <param name="Name">名称</param>
/// <param name="Value">数量</param>
public sealed record DashboardNameValue(string Name, int Value);