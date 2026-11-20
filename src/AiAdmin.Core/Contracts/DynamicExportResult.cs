namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表导出结果，记录已按系统设置的上限截取
/// </summary>
/// <typeparam name="T">记录类型，与列表接口返回的记录一致</typeparam>
/// <param name="Records">允许导出的记录</param>
/// <param name="Limit">单次导出上限</param>
/// <param name="Total">符合查询条件的记录总数</param>
public sealed record DynamicExportResult<T>(IReadOnlyList<T> Records, int Limit, int Total);