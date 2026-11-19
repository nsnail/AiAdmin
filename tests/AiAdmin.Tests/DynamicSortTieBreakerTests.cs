using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证次级排序键与主排序同向，使创建时间相同的记录在倒序和正序下分页都稳定
/// </summary>
public sealed class DynamicSortTieBreakerTests
{
    private static readonly DateTime _sameTime = new(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    ///     验证判断排序方向的辅助方法在默认、倒序和正序下的结果
    /// </summary>
    [Fact]
    public void IsSortDescending_ResolvesDefaultAndExplicitDirection() {
        Assert.True(DynamicFilterExtensions.IsSortDescending(null, null, true));
        Assert.False(DynamicFilterExtensions.IsSortDescending(null, null));
        Assert.True(DynamicFilterExtensions.IsSortDescending("CreatedAt", "DESC"));
        Assert.False(DynamicFilterExtensions.IsSortDescending("CreatedAt", "asc", true));
    }

    /// <summary>
    ///     验证未指定排序字段时使用默认倒序，次级排序键同为倒序
    /// </summary>
    [Fact]
    public void ThenByKey_DefaultDescendingFollowsPrimaryDirection() {
        var ids = Sort(null, null).Select(x => x.Id).ToList();
        Assert.Equal([4L, 3L, 2L, 1L], ids);
    }

    /// <summary>
    ///     验证显式倒序和正序时次级排序键方向与主排序一致
    /// </summary>
    /// <param name="order">排序方向</param>
    /// <param name="expected">期望的主键顺序</param>
    [Theory]
    [InlineData("desc", new long[] { 4, 3, 2, 1 })]
    [InlineData("asc", new long[] { 1, 2, 3, 4 })]
    public void ThenByKey_ExplicitOrderFollowsPrimaryDirection(
        string order
        , long[] expected
    ) {
        Assert.Equal(expected, Sort("CreatedAt", order).Select(x => x.Id).ToArray());
    }

    /// <summary>
    ///     对创建时间全部相同的记录应用动态排序并追加主键次级排序
    /// </summary>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <returns>排序后的记录</returns>
    private static IQueryable<Row> Sort(
        string? sortField
        , string? sortOrder
    ) {
        var rows = new[] { new Row(2, _sameTime), new Row(4, _sameTime), new Row(1, _sameTime), new Row(3, _sameTime) }.AsQueryable();
        Assert.All(rows, x => Assert.Equal(_sameTime, x.CreatedAt));
        var sorted = (IOrderedQueryable<Row>)rows.ApplyDynamicSort(sortField, sortOrder, nameof(Row.CreatedAt), true);
        return sorted.ThenByKey(x => x.Id, sortField, sortOrder, true);
    }

    /// <summary>
    ///     测试用记录
    /// </summary>
    /// <param name="Id">主键</param>
    /// <param name="CreatedAt">创建时间</param>
    private sealed record Row(long Id, DateTime CreatedAt);
}