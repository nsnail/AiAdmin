using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户 API Key 实体，每个用户最多一条，只保存哈希不保存明文
/// </summary>
public sealed class UserApiKey : EntityBase
{
    /// <summary>
    ///     API Key 主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     API Key 的 SHA-256 十六进制哈希
    /// </summary>
    public required string KeyHash { get; set; }

    /// <summary>
    ///     API Key 明文前缀，仅用于界面识别
    /// </summary>
    public required string KeyPrefix { get; set; }

    /// <summary>
    ///     最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     请求签名 Secret 的 AES-GCM 密文，服务端验签需要还原明文，因此不能只存哈希
    /// </summary>
    public string SignSecret { get; set; } = string.Empty;

    /// <summary>
    ///     所属用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     所属用户主键
    /// </summary>
    public long UserId { get; init; }
}