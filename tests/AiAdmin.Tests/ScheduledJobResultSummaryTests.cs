using System.Reflection;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证计划作业执行结果摘要：取响应体的前若干字符，去掉首尾空白，超长时截断，没有响应体时为空
/// </summary>
public sealed class ScheduledJobResultSummaryTests
{
    /// <summary>
    ///     没有响应体时摘要为空字符串，不会抛出空引用
    /// </summary>
    [Fact]
    public void SummaryIsEmptyWithoutBody() {
        Assert.Equal(string.Empty, Summarize(null));
        Assert.Equal(string.Empty, Summarize(string.Empty));
        Assert.Equal(string.Empty, Summarize("   \n"));
    }

    /// <summary>
    ///     短响应体原样保留，首尾空白被去掉
    /// </summary>
    [Fact]
    public void SummaryKeepsShortBodyAndTrimsWhitespace() {
        Assert.Equal("{\"code\":200}", Summarize("  {\"code\":200}\r\n"));
        Assert.Equal("OK", Summarize("OK"));
    }

    /// <summary>
    ///     恰好等于最大长度时不截断，超过一个字符时截断到最大长度，且保留的是前面的内容
    /// </summary>
    [Fact]
    public void SummaryTruncatesAtMaxLength() {
        var exact = new string('a', ScheduledJobExecution.RESULT_MAX_LENGTH);
        Assert.Equal(exact, Summarize(exact));

        var over = exact + "b";
        var truncated = Summarize(over);
        Assert.Equal(ScheduledJobExecution.RESULT_MAX_LENGTH, truncated.Length);
        Assert.Equal(exact, truncated);
    }

    /// <summary>
    ///     通过反射调用调度器的私有摘要方法
    /// </summary>
    /// <param name="responseBody">HTTP 响应体</param>
    /// <returns>执行结果摘要</returns>
    private static string Summarize(string? responseBody) {
        var method = typeof(ScheduledJobHostedService).GetMethod("Summarize", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [responseBody])!;
    }
}