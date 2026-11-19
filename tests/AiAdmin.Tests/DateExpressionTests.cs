using System.Globalization;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证动态筛选日期表达式的求值、安全限制以及在各查询路径中的生效情况
/// </summary>
public sealed class DateExpressionTests
{
    private static readonly string[] _lastTwoDaysRange = ["DateTime.Now.Date.AddDays(-1)", "DateTime.Now.Date.AddDays(1)"];
    private static readonly DateTimeOffset _now = new(2026, 3, 15, 14, 30, 45, TimeSpan.FromHours(8));
    private static readonly string[] _sameDayRange = ["DateTime.Now.Date", "DateTime.Now.Date"];
    private static readonly string[] _todayRange = ["DateTime.Now.Date", "DateTime.Now.Date.AddDays(1)"];

    /// <summary>
    ///     验证非法表达式返回明确的校验错误
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_RejectsInvalidExpression() {
        var roles = new[] { new Role { Id = 1L, Name = "A", Code = "A", CreatedAt = DateTime.UtcNow } }.AsQueryable();
        var filter = Condition(nameof(Role.CreatedAt), "GreaterThan", "DateTime.Now.Foo");
        _ = Assert.Throws<DynamicFilterValidationException>(() => roles.ApplyDynamicFilter(filter).ToList());
    }

    /// <summary>
    ///     验证实体查询的日期范围支持表达式，今日范围左闭右开
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_SupportsExpressionDateRange() {
        var today = DateTime.Now.Date;
        var roles = new[]
        {
            new Role { Id = 1L, Name = "Yesterday", Code = "A", CreatedAt = today.AddDays(-1).AddHours(23).ToUniversalTime() }
            , new Role { Id = 2L, Name = "Today", Code = "B", CreatedAt = today.AddHours(1).ToUniversalTime() }
            , new Role { Id = 3L, Name = "Tomorrow", Code = "C", CreatedAt = today.AddDays(1).AddHours(1).ToUniversalTime() }
        }.AsQueryable();
        var filter = Condition(nameof(Role.CreatedAt), "DateRange", _todayRange);
        Assert.Equal([2L], [.. roles.ApplyDynamicFilter(filter).Select(x => x.Id)]);
    }

    /// <summary>
    ///     验证比较操作符的值也支持表达式
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_SupportsExpressionInComparison() {
        var roles = new[]
        {
            new Role { Id = 1L, Name = "Old", Code = "A", CreatedAt = DateTime.UtcNow.AddDays(-3) }
            , new Role { Id = 2L, Name = "Recent", Code = "B", CreatedAt = DateTime.UtcNow.AddHours(-1) }
        }.AsQueryable();
        var filter = Condition(nameof(Role.CreatedAt), "GreaterThanOrEqual", "DateTime.Now.AddDays(-1)");
        Assert.Equal([2L], [.. roles.ApplyDynamicFilter(filter).Select(x => x.Id)]);
    }

    /// <summary>
    ///     验证起止都是今日零点时按整天处理，与选择同一天的行为一致
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_TreatsSameExpressionDayAsWholeDay() {
        var today = DateTime.Now.Date;
        var roles = new[]
        {
            new Role { Id = 1L, Name = "Yesterday", Code = "A", CreatedAt = today.AddMinutes(-1).ToUniversalTime() }
            , new Role { Id = 2L, Name = "Today", Code = "B", CreatedAt = today.AddHours(23).ToUniversalTime() }
        }.AsQueryable();
        var filter = Condition(nameof(Role.CreatedAt), "DateRange", _sameDayRange);
        Assert.Equal([2L], [.. roles.ApplyDynamicFilter(filter).Select(x => x.Id)]);
    }

    /// <summary>
    ///     验证 Elasticsearch 查询转换支持表达式
    /// </summary>
    [Fact]
    public void Build_SupportsExpressionDateRangeForElasticsearch() {
        var request = new DynamicQueryRequest { Current = 1, Size = 10, DynamicFilter = Condition("CreatedAt", "DateRange", _todayRange) };
        var json = ElasticsearchDynamicQuery.Build(request);
        Assert.Contains("\"gte\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime", json, StringComparison.Ordinal);
    }

    /// <summary>
    ///     验证请求日志的分表范围解析也支持表达式
    /// </summary>
    [Fact]
    public void Resolve_SupportsExpressionRangeForRequestLogs() {
        var filter = Condition("CreatedAt", "DateRange", _lastTwoDaysRange);
        var (from, to) = ApiRequestLogQueryRange.Resolve(filter, DateTime.UtcNow);
        var start = DateTime.Now.Date.AddDays(-1).ToUniversalTime();
        var end = DateTime.Now.Date.AddDays(1).ToUniversalTime();
        Assert.Equal(start.Date, from);
        Assert.Equal(end.TimeOfDay == TimeSpan.Zero ? end.AddTicks(-1).Date : end.Date, to);
    }

    /// <summary>
    ///     验证 AddDays 等运算的结果
    /// </summary>
    /// <param name="expression">日期表达式</param>
    /// <param name="expected">期望的结果文本</param>
    [Theory]
    [InlineData("DateTime.Now.Date", "2026-03-15T00:00:00+08:00")]
    [InlineData("DateTime.Today", "2026-03-15T00:00:00+08:00")]
    [InlineData("DateTime.Now.Date.AddDays(1)", "2026-03-16T00:00:00+08:00")]
    [InlineData("DateTime.Now.Date.AddDays(-7)", "2026-03-08T00:00:00+08:00")]
    [InlineData("DateTime.Now.Date.AddMonths(-1)", "2026-02-15T00:00:00+08:00")]
    [InlineData("DateTime.Now.Date.AddYears(1)", "2027-03-15T00:00:00+08:00")]
    [InlineData("DateTime.Now.AddHours(-2)", "2026-03-15T12:30:45+08:00")]
    [InlineData("DateTime.Now.AddMinutes(30)", "2026-03-15T15:00:45+08:00")]
    [InlineData("DateTime.Now", "2026-03-15T14:30:45+08:00")]
    [InlineData(" System.DateTime . Now . Date ", "2026-03-15T00:00:00+08:00")]
    [InlineData("DateTimeOffset.Now.Date", "2026-03-15T00:00:00+08:00")]
    [InlineData("DateTime.UtcNow.Date", "2026-03-15T00:00:00+00:00")]
    public void TryResolve_EvaluatesSupportedExpressions(
        string expression
        , string expected
    ) {
        Assert.True(DateExpression.TryResolve(expression, _now, out var result));
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), result);
    }

    /// <summary>
    ///     验证超长文本不按表达式处理
    /// </summary>
    [Fact]
    public void TryResolve_IgnoresOverlongText() {
        Assert.False(DateExpression.TryResolve("DateTime.Now" + new string('.', 200), _now, out _));
    }

    /// <summary>
    ///     验证不在白名单内的成员、方法和注入内容被拒绝，而不是被执行
    /// </summary>
    /// <param name="expression">非法表达式</param>
    [Theory]
    [InlineData("DateTime.Now.Foo")]
    [InlineData("DateTime.Foo")]
    [InlineData("DateTime.Now.Date.AddDays(abc)")]
    [InlineData("DateTime.Now.Date.AddDays(1")]
    [InlineData("DateTime.Now.Date.AddDays(1).")]
    [InlineData("DateTime.Now.Date..AddDays(1)")]
    [InlineData("DateTime.Now(1)")]
    [InlineData("DateTime.Now.Date.ToString()")]
    [InlineData("DateTime.Now.Date.AddDays(Environment.Exit(1))")]
    [InlineData("DateTime.Now.Date.AddMonths(1.5)")]
    [InlineData("DateTime.Now.Date.AddYears(99999)")]
    [InlineData("DateTime.")]
    public void TryResolve_RejectsUnsupportedExpressions(string expression) {
        _ = Assert.Throws<DynamicFilterValidationException>(() => DateExpression.TryResolve(expression, _now, out _));
    }

    /// <summary>
    ///     验证普通日期文本不被当作表达式
    /// </summary>
    /// <param name="text">日期文本</param>
    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2026-01-01T00:00:00Z")]
    [InlineData("2026-01-01T00:00:00.000+08:00")]
    [InlineData("hello")]
    public void TryResolve_ReturnsFalseForPlainDates(string text) {
        Assert.False(DateExpression.TryResolve(text, _now, out _));
    }

    /// <summary>
    ///     创建单条件动态筛选
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="operation">操作符</param>
    /// <param name="value">筛选值</param>
    /// <returns>动态筛选节点</returns>
    private static DynamicFilter Condition(
        string field
        , string operation
        , object value
    ) {
        return new DynamicFilter { Field = field, Operator = operation, Value = JsonSerializer.SerializeToElement(value) };
    }
}