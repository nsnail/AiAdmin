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