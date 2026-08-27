namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口列表项
/// </summary>
/// <param name="Id">接口标识</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="Name">接口名称</param>
/// <param name="AllowAnonymous">是否允许匿名访问</param>
/// <param name="Method">请求方法</param>
/// <param name="Path">请求路径</param>
/// <param name="Controller">控制器名称</param>
/// <param name="ControllerName">控制器显示名称</param>
/// <param name="Action">操作名称</param>
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