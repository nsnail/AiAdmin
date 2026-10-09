using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace AiAdmin.Api.Services;

/// <summary>
///     使用统一配置提供 AES-GCM 加密和解密能力
/// </summary>
/// <param name="configuration">应用配置</param>
public sealed class EncryptionService(IConfiguration configuration)
{
    private const string _ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int _GCM_NONCE_SIZE = 12;
    private const int _GCM_TAG_SIZE = 16;
    private readonly byte[] _key = CreateKey(configuration["Encryption:Key"]);

    /// <summary>
    ///     将 Base62 编码的字符串解码为字节数组
    /// </summary>
    /// <param name="value">Base62 编码字符串</param>
    /// <returns>解码后的字节数组</returns>
    /// <exception cref="FormatException">输入包含非 Base62 字符或载荷标记无效时抛出</exception>
    public static byte[] DecodeBase62(string value) {
        var number = BigInteger.Zero;
        foreach (var index in value.Select(c => _ALPHABET.IndexOf(c, StringComparison.Ordinal))) {
            if (index < 0) {
                throw new FormatException("Invalid Base62 character.");
            }

            number *= 62;
            number += index;
        }

        var framed = number.ToByteArray(true, true);
        return framed.Length < 2 || framed[0] != 1 ? throw new FormatException("Base62 payload marker is invalid.") : framed[1..];
    }

    /// <summary>
    ///     将字节数组编码为 Base62 字符串
    /// </summary>
    /// <param name="bytes">待编码字节</param>
    /// <returns>Base62 编码结果</returns>
    public static string EncodeBase62(byte[] bytes) {
        var value = new BigInteger([1, .. bytes], true, true);
        if (value == 0) {
            return _ALPHABET[0].ToString();
        }

        var chars = new StringBuilder();
        while (value > 0) {
            value = BigInteger.DivRem(value, 62, out var remainder);
            _ = chars.Append(_ALPHABET[(int)remainder]);
        }

        var result = chars.ToString().ToCharArray();
        Array.Reverse(result);
        return new string(result);
    }

    /// <summary>
    ///     解密包含随机数、认证标签和密文的 AES-GCM 字节载荷
    /// </summary>
    /// <param name="payload">AES-GCM 加密载荷</param>
    /// <returns>解密后的明文</returns>
    /// <exception cref="CryptographicException">载荷长度、认证标签或密钥无效时抛出</exception>
    public string Decrypt(ReadOnlySpan<byte> payload) {
        if (payload.Length < _GCM_NONCE_SIZE + _GCM_TAG_SIZE) {
            throw new CryptographicException("Encrypted payload is invalid.");
        }

        var nonce = payload[.._GCM_NONCE_SIZE];
        var tag = payload.Slice(_GCM_NONCE_SIZE, _GCM_TAG_SIZE);
        var cipher = payload[(_GCM_NONCE_SIZE + _GCM_TAG_SIZE)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, _GCM_TAG_SIZE);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    ///     将明文加密为包含随机数、认证标签和密文的字节载荷
    /// </summary>
    /// <param name="value">待加密明文</param>
    /// <returns>AES-GCM 加密载荷</returns>
    public byte[] Encrypt(string value) {
        var nonce = RandomNumberGenerator.GetBytes(_GCM_NONCE_SIZE);
        var plain = Encoding.UTF8.GetBytes(value);
        var cipher = new byte[plain.Length];
        var tag = new byte[_GCM_TAG_SIZE];
        using var aes = new AesGcm(_key, _GCM_TAG_SIZE);
        aes.Encrypt(nonce, plain, cipher, tag);
        return [.. nonce, .. tag, .. cipher];
    }

    /// <summary>
    ///     从配置值派生固定长度的 AES 密钥
    /// </summary>
    /// <param name="configuredKey">配置的原始加密密钥</param>
    /// <returns>SHA-256 派生的 AES 密钥</returns>
    /// <exception cref="InvalidOperationException">未配置有效加密密钥时抛出</exception>
    private static byte[] CreateKey(string? configuredKey) {
        return string.IsNullOrWhiteSpace(configuredKey)
            ? throw new InvalidOperationException("Encryption:Key is required.")
            : SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }
}