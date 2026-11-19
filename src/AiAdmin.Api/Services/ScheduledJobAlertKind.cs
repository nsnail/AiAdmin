namespace AiAdmin.Api.Services;

/// <summary>
///     计划作业报警动作
/// </summary>
public enum ScheduledJobAlertKind
{
    /// <summary>
    ///     无需通知
    /// </summary>
    None = 0

    ,

    /// <summary>
    ///     触发报警
    /// </summary>
    Raised = 1

    ,

    /// <summary>
    ///     解除报警
    /// </summary>
    Resolved = 2
}