namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口数据类型文档
/// </summary>
public sealed record ApiDocumentationType(string Name, string Type, string Description, IReadOnlyList<ApiDocumentationProperty> Properties);