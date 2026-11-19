using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证菜单按钮权限的读取与按角色授予过滤
/// </summary>
public sealed class MenuAuthListFilterTests
{
    private const string _META = """{"title":"t","keepAlive":true,"authList":[{"title":"导入","authMark":"import"},{"title":"新增","authMark":"add"}]}""";

    /// <summary>
    ///     验证菜单没有声明 authList 时不会凭空生成该字段
    /// </summary>
    [Fact]
    public void Filter_DoesNotAddAuthListWhenMenuHasNone() {
        var result = MenuAuthListFilter.Filter("""{"title":"t"}""", 1L, new HashSet<(long, string)> { (1L, "import") });
        Assert.False(result.TryGetProperty("authList", out _));
    }

    /// <summary>
    ///     验证只保留角色已授予的按钮，其余元数据原样保留
    /// </summary>
    [Fact]
    public void Filter_KeepsOnlyGrantedMarksAndPreservesOtherMeta() {
        var granted = new HashSet<(long, string)> { (1L, "import"), (2L, "add") };
        var result = MenuAuthListFilter.Filter(_META, 1L, granted);
        var marks = result.GetProperty("authList").EnumerateArray().Select(x => x.GetProperty("authMark").GetString()).ToList();
        Assert.Equal(["import"], marks);
        Assert.Equal("t", result.GetProperty("title").GetString());
        Assert.True(result.GetProperty("keepAlive").GetBoolean());
    }

    /// <summary>
    ///     验证没有授予任何按钮时 authList 为空数组，别的菜单同名标识的授予不会串用
    /// </summary>
    [Fact]
    public void Filter_ReturnsEmptyListWhenNothingGrantedForMenu() {
        var granted = new HashSet<(long, string)> { (2L, "import"), (2L, "add") };
        var result = MenuAuthListFilter.Filter(_META, 1L, granted);
        Assert.Empty(result.GetProperty("authList").EnumerateArray());
    }

    /// <summary>
    ///     验证读取 authList 声明的标识，空元数据和缺失字段返回空集合
    /// </summary>
    /// <param name="meta">菜单元数据 JSON</param>
    /// <param name="expected">期望读取到的标识数量</param>
    [Theory]
    [InlineData("""{"authList":[{"authMark":"import"},{"authMark":"add"}]}""", 2)]
    [InlineData("""{"title":"t"}""", 0)]
    [InlineData("", 0)]
    [InlineData("""{"authList":"x"}""", 0)]
    public void ReadAuthMarks_ReadsDeclaredMarks(
        string meta
        , int expected
    ) {
        Assert.Equal(expected, MenuAuthListFilter.ReadAuthMarks(meta).Count);
    }
}