using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证请求签名算法与前端固定向量一致，以及随机数去重、密钥派生的基本性质
/// </summary>
public sealed class RequestSignatureTests
{
    /// <summary>
    ///     验证不同会话派生不同密钥，篡改任一字段都会使签名变化
    /// </summary>
    [Fact]
    public void Sign_ChangesWhenAnyInputChanges() {
        Assert.NotEqual(RequestSignature.DeriveSigningKey("secret", "a"), RequestSignature.DeriveSigningKey("secret", "b"));
        var key = RequestSignature.DeriveSigningKey("secret", "a");
        var baseline = RequestSignature.Sign(key, RequestSignature.BuildStringToSign("POST", "/api/x", "1", "nonce", "hash"));
        Assert.NotEqual(baseline, RequestSignature.Sign(key, RequestSignature.BuildStringToSign("POST", "/api/y", "1", "nonce", "hash")));
        Assert.NotEqual(baseline, RequestSignature.Sign(key, RequestSignature.BuildStringToSign("POST", "/api/x", "1", "nonce", "other")));
        Assert.True(RequestSignature.FixedTimeEquals(baseline, baseline.ToUpperInvariant()));
        Assert.False(RequestSignature.FixedTimeEquals(baseline, new string('0', 64)));
    }

    /// <summary>
    ///     验证待签名串与 HMAC 结果和前端 request-signature.test.ts 的向量一致
    /// </summary>
    [Fact]
    public void Sign_MatchesFrontendVector() {
        var bodyHash = RequestSignature.HashBody("{\"a\":1}"u8);
        var content = RequestSignature.BuildStringToSign("post", "/api/x?y=1", "1700000000000", new string('n', 16), bodyHash);
        Assert.Equal("d856c5c8fb358b385d24a5189356ed19dfdd7f5ed05d646d6dbad197baefa344", RequestSignature.Sign("key", content));
    }
}