using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证 API Key 生成、哈希和展示前缀
/// </summary>
public sealed class ApiKeyServiceTests
{
    /// <summary>
    ///     验证生成的 Key 带固定前缀、足够长且每次不同
    /// </summary>
    [Fact]
    public void Generate_ReturnsPrefixedUniqueKeys() {
        var first = ApiKeyService.Generate();
        var second = ApiKeyService.Generate();
        Assert.StartsWith(ApiKeyService.KEY_PREFIX, first, StringComparison.Ordinal);
        Assert.True(first.Length >= 40);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
    }

    /// <summary>
    ///     验证展示前缀只取明文开头，不泄露完整 Key
    /// </summary>
    [Fact]
    public void GetDisplayPrefix_ReturnsShortPrefix() {
        var key = ApiKeyService.Generate();
        var prefix = ApiKeyService.GetDisplayPrefix(key);
        Assert.Equal(8, prefix.Length);
        Assert.StartsWith(prefix, key, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证哈希稳定且不同 Key 的哈希不同，重置覆盖哈希后旧 Key 无法匹配
    /// </summary>
    [Fact]
    public void Hash_IsStableAndDistinct() {
        var oldKey = ApiKeyService.Generate();
        var newKey = ApiKeyService.Generate();
        Assert.Equal(ApiKeyService.Hash(oldKey), ApiKeyService.Hash(oldKey));
        Assert.NotEqual(ApiKeyService.Hash(oldKey), ApiKeyService.Hash(newKey));
        Assert.Equal(64, ApiKeyService.Hash(oldKey).Length);
    }
}