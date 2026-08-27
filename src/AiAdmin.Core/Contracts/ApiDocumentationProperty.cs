namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口数据类型属性文档
/// </summary>
/// <param name="Name">属性名称</param>
/// <param name="Type">属性类型</param>
/// <param name="Required">是否必填</param>
/// <param name="Description">属性描述</param>
public sealed record ApiDocumentationProperty(string Name, string Type, bool Required, string Description);