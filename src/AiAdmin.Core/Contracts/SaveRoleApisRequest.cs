// 定义接口管理页面使用的查询、同步和授权请求响应模型。
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