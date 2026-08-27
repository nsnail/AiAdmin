namespace AiAdmin.Api.Contracts;

/// <summary>
///     单个接口文档
/// </summary>
/// <param name="Method">请求方法</param>
/// <param name="Path">请求路径</param>
/// <param name="Name">接口名称</param>
/// <param name="Description">接口描述</param>
/// <param name="Controller">控制器名称</param>
/// <param name="Action">操作名称</param>
/// <param name="Parameters">参数列表</param>
/// <param name="RequestBody">请求体类型</param>
/// <param name="ResponseType">响应类型</param>
public sealed record ApiDocumentationItem(
    string Method
    , string Path
    , string Name
    , string Description
    , string Controller
    , string Action
    , IReadOnlyList<ApiDocumentationParameter> Parameters
    , ApiDocumentationType? RequestBody
    , ApiDocumentationType? ResponseType);