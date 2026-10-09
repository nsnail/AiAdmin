using System.Security.Cryptography;

namespace AiAdmin.Api.Services;

/// <summary>
///     统一保护和解保护邮箱账号敏感凭据
/// </summary>
/// <param name="encryptionService">统一 AES-GCM 加解密服务</param>
public sealed class CredentialProtectionService(EncryptionService encryptionService)
{
    /// <summary>
    ///     使用 AES-GCM 加密敏感凭据
    /// </summary>
    /// <param name="value">明文凭据</param>
    /// <returns>加密后的凭据</returns>
    public string Protect(string? value) {
        return string.IsNullOrEmpty(value) ? string.Empty : Convert.ToBase64String(encryptionService.Encrypt(value));
    }

    /// <summary>
    ///     解密 AES-GCM 凭据并兼容历史明文数据
    /// </summary>
    /// <param name="value">加密凭据或历史明文</param>
    /// <returns>明文凭据</returns>
    public string Unprotect(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        try {
            var payload = Convert.FromBase64String(value);
            return encryptionService.Decrypt(payload);
        }
        catch (FormatException) {
            return value;
        }
        catch (CryptographicException) {
            return value;
        }
    }
}