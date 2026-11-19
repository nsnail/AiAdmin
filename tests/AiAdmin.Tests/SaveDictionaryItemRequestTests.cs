using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证字典内容保存请求的校验：前端请求层会移除值为空字符串的字段，因此缺省或为空的 Value 必须能通过校验，标签仍然必填
/// </summary>
public sealed class SaveDictionaryItemRequestTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     标签为空仍然不通过校验
    /// </summary>
    [Fact]
    public void EmptyLabelIsRejected() {
        var request = new SaveDictionaryItemRequest { Label = string.Empty, Sort = 1 };

        Assert.Contains(Validate(request), x => x.MemberNames.Contains(nameof(SaveDictionaryItemRequest.Label)));
    }

    /// <summary>
    ///     请求体缺少 value（前端把空字符串移除后的形态）时校验通过，且值为空字符串
    /// </summary>
    [Fact]
    public void MissingValueIsAcceptedAsEmptyString() {
        var request = JsonSerializer.Deserialize<SaveDictionaryItemRequest>(
            """{"label":"SMTP Host","sort":3,"isEnabled":true,"remark":"SMTP server host","id":"460372767211522"}""", _jsonOptions
        )!;

        Assert.Equal(string.Empty, request.Value);
        Assert.Empty(Validate(request));
    }

    /// <summary>
    ///     超过 4000 个字符的值仍然不通过校验
    /// </summary>
    [Fact]
    public void OverlongValueIsRejected() {
        var request = new SaveDictionaryItemRequest { Label = "x", Sort = 1, Value = new string('a', 4001) };

        Assert.Contains(Validate(request), x => x.MemberNames.Contains(nameof(SaveDictionaryItemRequest.Value)));
    }

    /// <summary>
    ///     对请求对象执行数据注解校验
    /// </summary>
    /// <param name="request">字典内容保存请求</param>
    /// <returns>校验失败项</returns>
    private static List<ValidationResult> Validate(SaveDictionaryItemRequest request) {
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}