namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口同步统计结果
/// </summary>
/// <param name="Added">新增数量</param>
/// <param name="Updated">更新数量</param>
/// <param name="Deleted">删除数量</param>
/// <param name="Total">总数量</param>
public sealed record ApiSyncResult(int Added, int Updated, int Deleted, int Total);