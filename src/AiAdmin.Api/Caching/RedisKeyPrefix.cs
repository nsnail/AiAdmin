namespace AiAdmin.Api.Caching;

/// <summary>
///     定义应用内部 Redis 键的统一前缀
/// </summary>
public static class RedisKeyPrefix
{
    /// <summary>
    ///     应用内部 Redis 键前缀
    /// </summary>
    public const string Value = "aiadmin:";
}