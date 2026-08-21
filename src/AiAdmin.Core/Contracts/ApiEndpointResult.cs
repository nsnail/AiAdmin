// 定义接口管理页面使用的查询、同步和授权请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口列表项
/// </summary>
public sealed record ApiEndpointResult(
    long Id
    , DateTimeOffset CreatedAt
    , string Name
    , bool AllowAnonymous
    , string Method
    , string Path
    , string Controller
    , string ControllerName
    , string Action);