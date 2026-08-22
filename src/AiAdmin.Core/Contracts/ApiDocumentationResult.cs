namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户可访问的接口文档集合
/// </summary>
public sealed record ApiDocumentationResult(IReadOnlyList<ApiDocumentationGroup> Groups);