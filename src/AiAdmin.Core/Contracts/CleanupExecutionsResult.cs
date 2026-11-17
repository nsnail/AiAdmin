namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理计划作业执行记录结果
/// </summary>
/// <param name="Hours">保留小时数</param>
/// <param name="Deleted">本次删除的执行记录数量</param>
public sealed record CleanupExecutionsResult(int Hours, int Deleted);