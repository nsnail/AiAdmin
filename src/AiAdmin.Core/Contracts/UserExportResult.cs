namespace AiAdmin.Api.Contracts;

/// <summary>
///     用户数据导出结果
/// </summary>
/// <param name="Records">允许导出的用户记录</param>
/// <param name="Limit">单次导出上限</param>
/// <param name="Total">符合查询条件的记录总数</param>
public sealed record UserExportResult(IReadOnlyList<UserListItem> Records, int Limit, int Total);