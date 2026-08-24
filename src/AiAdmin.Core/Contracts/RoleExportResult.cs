namespace AiAdmin.Api.Contracts;

/// <summary>
///     角色数据导出结果
/// </summary>
/// <param name="Records">允许导出的角色记录</param>
/// <param name="Limit">单次导出上限</param>
/// <param name="Total">符合查询条件的记录总数</param>
public sealed record RoleExportResult(IReadOnlyList<RoleListItem> Records, int Limit, int Total);