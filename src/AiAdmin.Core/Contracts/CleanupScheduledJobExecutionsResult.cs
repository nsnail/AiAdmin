namespace AiAdmin.Api.Contracts;

/// <summary>
///     清理计划作业执行记录结果
/// </summary>
/// <param name="DeletedCount">删除记录数量</param>
/// <param name="Cutoff">记录保留截止时间</param>
public sealed record CleanupScheduledJobExecutionsResult(int DeletedCount, DateTimeOffset Cutoff);