namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户 API Key 状态响应
/// </summary>
/// <param name="Exists">是否已生成 API Key</param>
/// <param name="Prefix">API Key 明文前缀</param>
/// <param name="CreatedAt">生成时间</param>
/// <param name="LastUsedAt">最后使用时间</param>
/// <param name="ApiKey">新生成的明文 API Key，仅在生成或重置时返回一次</param>
/// <param name="SignSecret">新生成的明文签名 Secret，仅在生成或重置时返回一次</param>
public sealed record ApiKeyStatusResult(
    bool Exists
    , string Prefix
    , DateTimeOffset? CreatedAt
    , DateTimeOffset? LastUsedAt
    , string? ApiKey
    , string? SignSecret);