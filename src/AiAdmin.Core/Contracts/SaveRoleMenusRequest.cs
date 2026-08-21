namespace AiAdmin.Api.Contracts;

/// <summary>
///     保存角色菜单授权请求
/// </summary>
public sealed class SaveRoleMenusRequest
{
    /// <summary>
    ///     菜单主键集合
    /// </summary>
    public long[] MenuIds { get; init; } = [];
}