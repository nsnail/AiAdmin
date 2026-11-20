using System.Security.Cryptography;
using System.Text;

namespace AiAdmin.Api.Services;

/// <summary>
///     API Key 生成与哈希工具
/// </summary>
public static class ApiKeyService
{
    /// <summary>
    ///     Authorization 请求头中的 API Key 方案名称
    /// </summary>
    public const string AUTHORIZATION_SCHEME = "ApiKey";

    /// <summary>
    ///     API Key 请求头名称
    /// </summary>
    public const string HEADER_NAME = "X-Api-Key";

    /// <summary>
    ///     API Key 固定前缀
    /// </summary>
    public const string KEY_PREFIX = "tk_";

    /// <summary>
    ///     请求签名 Secret 固定前缀
    /// </summary>
    public const string SECRET_PREFIX = "sk_";

    /// <summary>
    ///     界面展示的明文前缀长度
    /// </summary>
    private const int _DISPLAY_PREFIX_LENGTH = 8;

    /// <summary>
    ///     生成新的随机 API Key
    /// </summary>
    /// <returns>以固定前缀开头的明文 API Key</returns>
    public static string Generate() {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return KEY_PREFIX + random;
    }

    /// <summary>
    ///     生成新的请求签名 Secret，与 API Key 相互独立，只用于计算 HMAC，不随请求发送
    /// </summary>
    /// <returns>以固定前缀开头的明文 Secret</returns>
    public static string GenerateSecret() {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return SECRET_PREFIX + random;
    }

    /// <summary>
    ///     截取用于界面识别的明文前缀
    /// </summary>
    /// <param name="apiKey">明文 API Key</param>
    /// <returns>明文前缀</returns>
    public static string GetDisplayPrefix(string apiKey) {
        return apiKey[.._DISPLAY_PREFIX_LENGTH];
    }

    /// <summary>
    ///     计算 API Key 的 SHA-256 十六进制哈希
    /// </summary>
    /// <param name="apiKey">明文 API Key</param>
    /// <returns>大写十六进制哈希</returns>
    public static string Hash(string apiKey) {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
    }
}