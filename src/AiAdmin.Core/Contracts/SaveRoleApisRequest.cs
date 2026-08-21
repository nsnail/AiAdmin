namespace AiAdmin.Api.Contracts;

/// <summary>
///     保存角色接口授权请求
/// </summary>
public sealed class SaveRoleApisRequest
{
    /// <summary>
    ///     接口主键集合
    /// </summary>
    public long[] ApiIds { get; init; } = [];
}