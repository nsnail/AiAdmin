namespace AiAdmin.Api.Services;

/// <summary>
///     保存在 Redis 中的幂等记录：占位阶段只有请求指纹，完成后包含可回放的响应
/// </summary>
/// <param name="Fingerprint">请求内容指纹</param>
/// <param name="Completed">是否已完成并保存了响应</param>
/// <param name="StatusCode">已保存响应的 HTTP 状态码</param>
/// <param name="Body">已保存响应的 JSON 文本</param>
public sealed record IdempotencyEntry(string Fingerprint, bool Completed, int StatusCode, string? Body);