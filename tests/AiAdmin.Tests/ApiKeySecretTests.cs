using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证 API Key 签名 Secret 的生成，以及它与 API Key 相互独立
/// </summary>
public sealed class ApiKeySecretTests
{
    /// <summary>
    ///     验证 Secret 带固定前缀、足够长、每次不同，且不能由 API Key 推导
    /// </summary>
    [Fact]
    public void GenerateSecret_IsPrefixedUniqueAndIndependentOfKey() {
        var key = ApiKeyService.Generate();
        var first = ApiKeyService.GenerateSecret();
        var second = ApiKeyService.GenerateSecret();
        Assert.StartsWith(ApiKeyService.SECRET_PREFIX, first, StringComparison.Ordinal);
        Assert.True(first.Length >= 40);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(key[ApiKeyService.KEY_PREFIX.Length..], first, StringComparison.Ordinal);
        Assert.NotEqual(ApiKeyService.Hash(key), first);
    }
}