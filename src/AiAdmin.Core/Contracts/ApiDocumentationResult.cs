namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户可访问的接口文档集合
/// </summary>
/// <param name="Groups">接口文档分组</param>
public sealed record ApiDocumentationResult(IReadOnlyList<ApiDocumentationGroup> Groups);