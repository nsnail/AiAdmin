namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口数据类型属性文档
/// </summary>
public sealed record ApiDocumentationProperty(string Name, string Type, bool Required, string Description);