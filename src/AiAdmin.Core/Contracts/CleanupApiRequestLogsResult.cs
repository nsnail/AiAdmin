namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理 API 请求日志分表结果
/// </summary>
/// <param name="Days">保留天数</param>
/// <param name="DroppedTables">本次删除的分表名称</param>
public sealed record CleanupApiRequestLogsResult(int Days, IReadOnlyList<string> DroppedTables);