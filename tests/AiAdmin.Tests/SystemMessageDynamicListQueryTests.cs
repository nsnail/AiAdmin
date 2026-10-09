using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证系统消息列表动态筛选、别名排序和分页协议
/// </summary>
public sealed class SystemMessageDynamicListQueryTests
{
    private static readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RecipientCount"] = "Recipients.Count"
    };

    private static readonly string[] _dateRange = ["2026-01-01T00:00:00Z", "2026-02-01T00:00:00Z"];

    private static readonly string[] _recipientTitles = ["Welcome", "Release"];

    /// <summary>
    ///     验证消息列表支持字符串、布尔、数值和日期范围筛选
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersSupportedMessageFieldTypes() {
        var filter = new DynamicFilter {
            Logic = "And", Filters = [
                Condition(nameof(SystemMessage.Title), "Contains", "Release")
                , Condition(nameof(SystemMessage.IsPopup), "Equal", true)
                , Condition(nameof(SystemMessage.RecipientCount), "GreaterThanOrEqual", 2)
                , Condition(nameof(SystemMessage.CreatedAt), "DateRange", _dateRange)
            ]
        };

        Assert.Equal([2L], [.. CreateMessages().ApplyDynamicFilter(filter, _aliases).Select(x => x.Id)]);
    }

    /// <summary>
    ///     验证消息列表支持集合值和嵌套 And 或 Or 条件
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_SupportsCollectionAndNestedGroups() {
        var filter = new DynamicFilter {
            Logic = "Or", Filters = [
                new DynamicFilter {
                    Logic = "And", Filters = [
                        Condition(nameof(SystemMessage.Title), "Any", _recipientTitles)
                        , Condition(nameof(SystemMessage.IsPopup), "Equal", false)
                    ]
                }
                , Condition(nameof(SystemMessage.RecipientCount), "Equal", 0)
            ]
        };

        Assert.Equal([1L, 3L], [.. CreateMessages().ApplyDynamicFilter(filter, _aliases).OrderBy(x => x.Id).Select(x => x.Id)]);
    }

    /// <summary>
    ///     验证收件人数别名支持服务端排序和稳定分页
    /// </summary>
    [Fact]
    public void ApplyDynamicSort_UsesRecipientCountAliasWithStablePaging() {
        var page = CreateMessages()
            .ApplyDynamicSort("RecipientCount", "desc", nameof(SystemMessage.CreatedAt), true, _aliases)
            .Skip(1)
            .Take(1)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1L], page);
    }

    /// <summary>
    ///     验证非法操作符和排序字段被拒绝
    /// </summary>
    [Fact]
    public void ApplyDynamicQuery_RejectsInvalidOperatorAndSortField() {
        var invalidOperator = Condition(nameof(SystemMessage.Title), "Unsupported", "Release");
        _ = Assert.Throws<DynamicFilterValidationException>(() => CreateMessages().ApplyDynamicFilter(invalidOperator, _aliases).ToArray());
        _ = Assert.Throws<DynamicFilterValidationException>(() => CreateMessages().ApplyDynamicSort("InvalidField", "asc", nameof(SystemMessage.CreatedAt), true, _aliases).ToArray());
    }

    /// <summary>
    ///     执行 Condition 方法对应的业务逻辑
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="operation">操作符</param>
    /// <param name="value">待处理的值</param>
    /// <returns>Condition 方法的执行结果</returns>
    private static DynamicFilter Condition(string field, string operation, object value) {
        return new DynamicFilter { Field = field, Operator = operation, Value = JsonSerializer.SerializeToElement(value) };
    }

    /// <summary>
    ///     创建 CreateMessages 方法对应的业务数据
    /// </summary>
    /// <returns>CreateMessages 方法的执行结果</returns>
    private static IQueryable<SystemMessage> CreateMessages() {
        return new[] {
            new SystemMessage {
                Id = 1L, SenderId = 1L, Title = "Welcome", CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
                , IsPopup = false
            }
            , new SystemMessage {
                Id = 2L, SenderId = 1L, Title = "Release notice", CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                , IsPopup = true, Recipients = { new UserMessage { UserId = 1L }, new UserMessage { UserId = 2L } }
            }
            , new SystemMessage {
                Id = 3L, SenderId = 1L, Title = "Maintenance", CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
                , IsPopup = false
            }
        }.AsQueryable();
    }
}