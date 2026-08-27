using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户列表项
/// </summary>
/// <param name="Id">用户主键</param>
/// <param name="Avatar">头像地址</param>
/// <param name="Status">状态编码</param>
/// <param name="UserName">登录用户名</param>
/// <param name="UserGender">性别编码</param>
/// <param name="UserPhone">联系电话</param>
/// <param name="UserEmail">电子邮箱</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="UserRoles">角色编码集合</param>
/// <param name="RoleNames">角色名称集合</param>
/// <param name="DepartmentIds">部门主键集合</param>
/// <param name="DepartmentNames">部门名称集合</param>
/// <param name="CreateBy">创建人</param>
/// <param name="CreateTime">创建时间</param>
/// <param name="UpdateBy">更新人</param>
/// <param name="UpdateTime">更新时间</param>
/// <param name="Version">并发版本号</param>
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
    , string[] RoleNames
    , long[] DepartmentIds
    , string[] DepartmentNames
    , string CreateBy
    , DateTimeOffset CreateTime
    , string UpdateBy
    , DateTimeOffset? UpdateTime
    , int Version);