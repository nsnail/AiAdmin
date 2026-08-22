namespace AiAdmin.Api.Contracts;

/// <summary>
///     单个接口文档
/// </summary>
public sealed record ApiDocumentationItem(
    string Method,
    string Path,
    string Name,
    string Description,
    string Controller,
    string Action,
    IReadOnlyList<ApiDocumentationParameter> Parameters,
    ApiDocumentationType? RequestBody,
    ApiDocumentationType? ResponseType);