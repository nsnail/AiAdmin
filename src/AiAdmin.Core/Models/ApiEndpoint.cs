using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     系统接口实体
/// </summary>
public sealed class ApiEndpoint : EntityBase
{
    /// <summary>
    ///     操作方法名称
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    ///     是否允许匿名访问
    /// </summary>
    [ListFilter("listFilter.api.allowAnonymous", "select", Options = ["true:listFilter.option.yes", "false:listFilter.option.no"])]
    public bool AllowAnonymous { get; set; }

    /// <summary>
    ///     控制器代码名称
    /// </summary>
    [ListFilter("listFilter.api.controller", Placeholder = "listFilter.placeholder.controller")]
    public string Controller { get; set; } = string.Empty;

    /// <summary>
    ///     控制器显示名称
    /// </summary>
    [ListFilter("listFilter.api.controllerName", Placeholder = "listFilter.placeholder.controllerName")]
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>
    ///     接口主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     HTTP 请求方法
    /// </summary>
    [ListFilter(
        "listFilter.api.method", "select"
        , Options =
        [
            "GET:listFilter.option.get", "POST:listFilter.option.post", "PUT:listFilter.option.put", "PATCH:listFilter.option.patch"
            , "DELETE:listFilter.option.delete"
        ]
    )]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    ///     接口显示名称
    /// </summary>
    [ListFilter("listFilter.api.name", Placeholder = "listFilter.placeholder.apiName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     接口路由路径
    /// </summary>
    [ListFilter("listFilter.api.path", Placeholder = "listFilter.placeholder.path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     角色接口关联集合
    /// </summary>
    public ICollection<RoleApi> RoleApis { get; init; } = [];
}