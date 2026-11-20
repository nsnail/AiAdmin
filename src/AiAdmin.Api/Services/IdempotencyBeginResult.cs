namespace AiAdmin.Api.Services;

/// <summary>
///     幂等检查结果，回放时携带已保存的响应
/// </summary>
/// <param name="State">检查状态</param>
/// <param name="Entry">已保存的记录，仅回放时有值</param>
public sealed record IdempotencyBeginResult(IdempotencyState State, IdempotencyEntry? Entry);