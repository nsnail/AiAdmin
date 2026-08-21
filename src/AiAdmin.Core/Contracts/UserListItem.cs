using AiAdmin.Api.Models;

// 定义用户查询和用户维护请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户列表项
/// </summary>
public sealed record UserListItem(
    long Id
    , string Avatar
    , string Status
    , string UserName
    , UserGender UserGender
    , string UserPhone
    , string UserEmail
    , bool IsEnabled
    , string[] UserRoles
    , long[] DepartmentIds
    , string[] DepartmentNames
    , string CreateBy
    , DateTimeOffset CreateTime
    , string UpdateBy
    , DateTimeOffset? UpdateTime);