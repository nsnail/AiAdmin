namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口数据类型文档
/// </summary>
/// <param name="Name">类型名称</param>
/// <param name="Type">类型标识</param>
/// <param name="Description">类型描述</param>
/// <param name="Properties">属性列表</param>
public sealed record ApiDocumentationType(string Name, string Type, string Description, IReadOnlyList<ApiDocumentationProperty> Properties);