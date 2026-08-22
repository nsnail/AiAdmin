namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口参数文档
/// </summary>
public sealed record ApiDocumentationParameter(string Name, string In, string Type, bool Required, string Description, string? DefaultValue);