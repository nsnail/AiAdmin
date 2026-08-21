namespace AiAdmin.Api.Contracts;

/// <summary>
///     已保存查询条件响应
/// </summary>
/// <param name="Id">查询条件主键</param>
/// <param name="Name">查询条件名称</param>
/// <param name="IsGlobal">是否为全局查询</param>
/// <param name="DynamicFilter">动态筛选根节点</param>
public sealed record SavedQueryResult(long Id, string Name, bool IsGlobal, DynamicFilter DynamicFilter);