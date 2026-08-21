namespace AiAdmin.Api.Models;

/// <summary>
///     角色与接口关联实体
/// </summary>
public sealed class RoleApi : EntityBase
{
    /// <summary>
    ///     关联接口
    /// </summary>
    public ApiEndpoint ApiEndpoint { get; init; } = null!;

    /// <summary>
    ///     接口主键
    /// </summary>
    public long ApiEndpointId { get; init; }

    /// <summary>
    ///     关联角色
    /// </summary>
    public Role Role { get; init; } = null!;

    /// <summary>
    ///     角色主键
    /// </summary>
    public long RoleId { get; init; }
}