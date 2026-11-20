using System.Text.Json;
using AiAdmin.Api.Logging;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证日志脱敏：消息头、JSON 和表单中的密码、令牌等敏感值被替换，普通字段和中文内容保持不变
/// </summary>
public sealed class SensitiveDataMaskerTests
{
    /// <summary>
    ///     验证表单体中的敏感键被替换
    /// </summary>
    [Fact]
    public void MaskBody_Form_MasksSensitivePairs() {
        var masked = SensitiveDataMasker.MaskBody("name=tom&password=secret1&new%5Fpassword=x&age=3", "application/x-www-form-urlencoded");
        Assert.Equal("name=tom&password=***&new%5Fpassword=***&age=3", masked);
    }

    /// <summary>
    ///     验证 JSON 被截断无法解析时仍用兜底规则脱敏
    /// </summary>
    [Fact]
    public void MaskBody_InvalidJson_FallsBackToPatternMasking() {
        var masked = SensitiveDataMasker.MaskBody("""{"name":"tom","token":"abc123","tail":"cut""", "application/json");
        Assert.DoesNotContain("abc123", masked, StringComparison.Ordinal);
        Assert.Contains("\"token\":\"***\"", masked, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"tom\"", masked, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证嵌套对象和数组中的敏感字段被替换，其余字段与中文保持原样
    /// </summary>
    [Fact]
    public void MaskBody_Json_MasksNestedSensitiveFields() {
        const string body
            = """{"UserName":"张三","Password":"p@ss","data":{"accessToken":"abc","items":[{"refresh_token":"r","id":1}]},"sessionData":{"a":1}}""";
        var masked = SensitiveDataMasker.MaskBody(body, "application/json; charset=utf-8");
        using var document = JsonDocument.Parse(masked);
        var root = document.RootElement;
        Assert.Equal("张三", root.GetProperty("UserName").GetString());
        Assert.Equal("***", root.GetProperty("Password").GetString());
        Assert.Equal("***", root.GetProperty("data").GetProperty("accessToken").GetString());
        Assert.Equal("***", root.GetProperty("data").GetProperty("items")[0].GetProperty("refresh_token").GetString());
        Assert.Equal(1, root.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal("***", root.GetProperty("sessionData").GetString());
        Assert.DoesNotContain("p@ss", masked, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证空内容、无内容类型和非 JSON、非表单内容原样返回
    /// </summary>
    [Fact]
    public void MaskBody_PassesThroughOtherContent() {
        Assert.Equal(string.Empty, SensitiveDataMasker.MaskBody(null, "application/json"));
        Assert.Equal("password=1", SensitiveDataMasker.MaskBody("password=1", null));
        Assert.Equal("password=1", SensitiveDataMasker.MaskBody("password=1", "text/plain"));
    }

    /// <summary>
    ///     验证敏感消息头被替换，普通消息头保留，名称匹配忽略大小写
    /// </summary>
    [Fact]
    public void MaskHeaders_MasksCredentialHeaders() {
        var json = SensitiveDataMasker.MaskHeaders(
            [
                KeyValuePair.Create("authorization", "Bearer abc"), KeyValuePair.Create("Cookie", "sid=1"), KeyValuePair.Create("X-Auth-Token", "t")
                , KeyValuePair.Create("Host", "example.com")
            ]
        );
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("***", root.GetProperty("authorization").GetString());
        Assert.Equal("***", root.GetProperty("Cookie").GetString());
        Assert.Equal("***", root.GetProperty("X-Auth-Token").GetString());
        Assert.Equal("example.com", root.GetProperty("Host").GetString());
    }
}