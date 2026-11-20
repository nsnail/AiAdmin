namespace AiAdmin.Api.Services;

/// <summary>
///     幂等检查的结果状态
/// </summary>
public enum IdempotencyState
{
    /// <summary>
    ///     首次请求，已占位，应继续执行业务
    /// </summary>
    Started = 0

    ,

    /// <summary>
    ///     相同的请求正在处理中
    /// </summary>
    InProgress = 1

    ,

    /// <summary>
    ///     相同的键已有成功结果，应直接回放
    /// </summary>
    Replay = 2

    ,

    /// <summary>
    ///     相同的键用于了不同的请求内容
    /// </summary>
    Mismatch = 3
}