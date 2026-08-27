using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色列表项
/// </summary>
/// <param name="RoleId">角色标识</param>
/// <param name="RoleName">角色名称</param>
/// <param name="RoleCode">角色编码</param>
/// <param name="Description">角色描述</param>
/// <param name="DataScope">数据范围</param>
/// <param name="Enabled">是否启用</param>
/// <param name="CreateTime">创建时间</param>
/// <param name="UpdateTime">更新时间</param>
public sealed record RoleListItem(
    long RoleId
    , string RoleName
    , string RoleCode
    , string Description
    , RoleDataScope DataScope
    , bool Enabled
    , DateTimeOffset CreateTime
    , DateTimeOffset? UpdateTime);