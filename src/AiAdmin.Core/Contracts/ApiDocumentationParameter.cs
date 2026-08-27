namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口参数文档
/// </summary>
/// <param name="Name">参数名称</param>
/// <param name="In">参数位置</param>
/// <param name="Type">参数类型</param>
/// <param name="Required">是否必填</param>
/// <param name="Description">参数描述</param>
/// <param name="DefaultValue">默认值</param>
public sealed record ApiDocumentationParameter(string Name, string In, string Type, bool Required, string Description, string? DefaultValue);