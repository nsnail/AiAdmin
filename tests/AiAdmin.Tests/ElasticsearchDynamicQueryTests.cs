using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Logging;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证系统日志动态查询转换：短 keyword 字段直接查询索引字段，长文本字段才使用运行时脚本
/// </summary>
public sealed class ElasticsearchDynamicQueryTests
{
    /// <summary>
    ///     验证按 Level 等短 keyword 字段筛选时不生成运行时脚本字段，避免大索引上逐文档执行脚本导致超时
    /// </summary>
    [Fact]
    public void Build_UsesIndexedFieldForShortKeywordFilter() {
        var request = new DynamicQueryRequest
        {
            DynamicFilter = new DynamicFilter
            {
                Filters = [new DynamicFilter { Field = "Level", Operator = "Equal", Value = JsonSerializer.SerializeToElement("Error") }]
            }
        };

        var json = ElasticsearchDynamicQuery.Build(request);

        using var document = JsonDocument.Parse(json);
        Assert.Empty(document.RootElement.GetProperty("runtime_mappings").EnumerateObject());
        Assert.Contains("\"term\":{\"level\":\"Error\"}", json, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证按 Message 等长文本字段筛选时仍使用运行时字段，保持原有筛选语义
    /// </summary>
    [Fact]
    public void Build_UsesRuntimeFieldForLongTextFilter() {
        var request = new DynamicQueryRequest
        {
            DynamicFilter = new DynamicFilter
            {
                Filters =
                [
                    new DynamicFilter { Field = "Message", Operator = "Contains", Value = JsonSerializer.SerializeToElement("timeout") }
                ]
            }
        };

        var json = ElasticsearchDynamicQuery.Build(request);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("runtime_mappings").TryGetProperty("filter_message", out _));
    }
}