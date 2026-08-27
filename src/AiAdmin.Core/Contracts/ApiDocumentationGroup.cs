namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口文档控制器分组
/// </summary>
/// <param name="Name">分组名称</param>
/// <param name="Description">分组描述</param>
/// <param name="Items">接口文档列表</param>
public sealed record ApiDocumentationGroup(string Name, string Description, IReadOnlyList<ApiDocumentationItem> Items);