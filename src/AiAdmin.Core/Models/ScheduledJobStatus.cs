namespace AiAdmin.Api.Models;

/// <summary>
///     计划作业执行状态
/// </summary>
public enum ScheduledJobStatus
{
    /// <summary>
    ///     等待执行
    /// </summary>
    Waiting = 0

    ,

    /// <summary>
    ///     执行中
    /// </summary>
    Running = 1

    ,

    /// <summary>
    ///     执行成功
    /// </summary>
    Success = 2

    ,

    /// <summary>
    ///     执行失败
    /// </summary>
    Failed = 3

    ,

    /// <summary>
    ///     执行超时
    /// </summary>
    Timeout = 4
}