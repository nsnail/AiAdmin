using System.Security.Cryptography;
using System.Text;

namespace AiAdmin.Api.Services;

/// <summary>
///     请求签名的公共算法：签名密钥派生、待签名串拼装、HMAC 计算与定长比较，前后端必须保持一致
/// </summary>
public static class RequestSignature
{
    /// <summary>
    ///     一次性随机数请求头
    /// </summary>
    public const string NONCE_HEADER = "X-Nonce";

    /// <summary>
    ///     请求签名请求头，值为小写十六进制 HMAC-SHA256
    /// </summary>
    public const string SIGNATURE_HEADER = "X-Signature";

    /// <summary>
    ///     请求时间戳请求头，值为 Unix 毫秒时间戳
    /// </summary>
    public const string TIMESTAMP_HEADER = "X-Timestamp";

    /// <summary>
    ///     无法在客户端计算摘要的请求体（如 multipart 上传）使用的占位摘要
    /// </summary>
    public const string UNSIGNED_PAYLOAD = "UNSIGNED-PAYLOAD";

    /// <summary>
    ///     拼装待签名字符串
    /// </summary>
    /// <param name="method">HTTP 方法，转为大写参与签名</param>
    /// <param name="rawTarget">原始请求目标，包含路径和查询字符串</param>
    /// <param name="timestamp">请求时间戳文本</param>
    /// <param name="nonce">一次性随机数</param>
    /// <param name="bodyHash">请求体摘要或占位摘要</param>
    /// <returns>以换行分隔的待签名字符串</returns>
    public static string BuildStringToSign(
        string method
        , string rawTarget
        , string timestamp
        , string nonce
        , string bodyHash
    ) {
        return string.Join('\n', method.ToUpperInvariant(), rawTarget, timestamp, nonce, bodyHash);
    }

    /// <summary>
    ///     由服务端密钥和签名会话标识派生 JWT 会话的签名密钥
    /// </summary>
    /// <param name="secret">服务端 JWT 密钥</param>
    /// <param name="sessionId">签名会话标识</param>
    /// <returns>小写十六进制签名密钥</returns>
    public static string DeriveSigningKey(
        string secret
        , string sessionId
    ) {
        return Sign(secret, "request-signing:" + sessionId);
    }

    /// <summary>
    ///     以定长方式比较签名，避免时序侧信道
    /// </summary>
    /// <param name="expected">期望签名</param>
    /// <param name="actual">请求携带的签名</param>
    /// <returns>两者一致时返回 true</returns>
    public static bool FixedTimeEquals(
        string expected
        , string actual
    ) {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual.ToLowerInvariant()));
    }

    /// <summary>
    ///     计算请求体的小写十六进制 SHA-256 摘要
    /// </summary>
    /// <param name="body">请求体字节</param>
    /// <returns>小写十六进制摘要</returns>
    public static string HashBody(ReadOnlySpan<byte> body) {
        return Convert.ToHexStringLower(SHA256.HashData(body));
    }

    /// <summary>
    ///     使用密钥对内容计算 HMAC-SHA256
    /// </summary>
    /// <param name="key">签名密钥，按 UTF-8 编码</param>
    /// <param name="content">待签名内容</param>
    /// <returns>小写十六进制签名</returns>
    public static string Sign(
        string key
        , string content
    ) {
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(content)));
    }
}