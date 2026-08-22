namespace AiAdmin.Api.Attributes;

/// <summary>
///     标记操作是否纳入接口文档
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ApiDocumentedAttribute : Attribute;