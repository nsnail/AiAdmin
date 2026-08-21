namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色列表项
/// </summary>
public sealed record RoleListItem(
    long RoleId
    , string RoleName
    , string RoleCode
    , string Description
    , string DataScope
    , bool Enabled
    , DateTimeOffset CreateTime);