namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口文档控制器分组
/// </summary>
public sealed record ApiDocumentationGroup(string Name, string Description, IReadOnlyList<ApiDocumentationItem> Items);