using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证限流规则选择：全局规则始终叠加，其余按“指定用户 > 角色 > 每个用户的默认规则”取最具体的一层，同层取额度最宽松的一条
/// </summary>
public sealed class RateLimitRuleSelectorTests
{
    private static readonly string[] _adminAndVip = ["R_ADMIN", "R_VIP"];
    private static readonly string[] _vip = ["R_VIP"];

    /// <summary>
    ///     全局规则始终生效，并排在前面
    /// </summary>
    [Fact]
    public void Select_AlwaysKeepsGlobalRules() {
        var rules = new[] { Common(1, 10), Global(2, 100) };
        Assert.Equal([2L, 1L], Ids(RateLimitRuleSelector.Select(rules, "7", [])));
    }

    /// <summary>
    ///     未登录的请求只适用全局规则和默认规则，不适用指定用户和角色规则
    /// </summary>
    [Fact]
    public void Select_AnonymousRequestOnlyGetsGlobalAndCommon() {
        var rules = new[] { SpecificUser(1, 7, 500), Role(2, "R_VIP", 1000), Common(3, 10), Global(4, 100) };
        Assert.Equal([4L, 3L], Ids(RateLimitRuleSelector.Select(rules, null, [])));
    }

    /// <summary>
    ///     额度相同时取主键较小的规则，结果稳定
    /// </summary>
    [Fact]
    public void Select_BreaksTiesByLowestId() {
        var rules = new[] { Role(9, "R_VIP", 100), Role(4, "R_ADMIN", 100) };
        Assert.Equal([4L], Ids(RateLimitRuleSelector.Select(rules, "7", _adminAndVip)));
    }

    /// <summary>
    ///     宽松程度按每秒允许的请求数比较，而不是只看请求数上限
    /// </summary>
    [Fact]
    public void Select_ComparesRateNotRawLimit() {
        var rules = new[] { Role(1, "R_ADMIN", 600), Role(2, "R_VIP", 100, 5) };
        Assert.Equal([2L], Ids(RateLimitRuleSelector.Select(rules, "7", _adminAndVip)));
    }

    /// <summary>
    ///     没有匹配的用户和角色规则时回落到默认规则
    /// </summary>
    [Fact]
    public void Select_FallsBackToCommon() {
        var rules = new[] { Common(1, 10), Role(2, "R_VIP", 1000), SpecificUser(3, 99, 500) };
        Assert.Equal([1L], Ids(RateLimitRuleSelector.Select(rules, "7", [])));
    }

    /// <summary>
    ///     用户同时拥有多个角色时，只取额度最宽松的一条角色规则
    /// </summary>
    [Fact]
    public void Select_PicksLoosestRuleAmongMatchedRoles() {
        var rules = new[] { Role(1, "R_ADMIN", 100), Role(2, "R_VIP", 1000), Role(3, "R_OTHER", 5000) };
        Assert.Equal([2L], Ids(RateLimitRuleSelector.Select(rules, "7", _adminAndVip)));
    }

    /// <summary>
    ///     角色规则优先于默认规则，让某个角色可以放宽额度
    /// </summary>
    [Fact]
    public void Select_PrefersRoleOverCommon() {
        var rules = new[] { Common(1, 10), Role(2, "R_VIP", 1000) };
        Assert.Equal([2L], Ids(RateLimitRuleSelector.Select(rules, "7", _vip)));
    }

    /// <summary>
    ///     指定用户的规则优先于角色规则和默认规则，其余两层不再叠加
    /// </summary>
    [Fact]
    public void Select_PrefersSpecificUserOverRoleAndCommon() {
        var rules = new[] { Common(1, 10), Role(2, "R_VIP", 50), SpecificUser(3, 7, 500) };
        Assert.Equal([3L], Ids(RateLimitRuleSelector.Select(rules, "7", _vip)));
    }

    /// <summary>
    ///     没有任何规则适用时不选出规则
    /// </summary>
    [Fact]
    public void Select_ReturnsNothingWhenNoRuleApplies() {
        var rules = new[] { Role(1, "R_ADMIN", 10) };
        Assert.Empty(RateLimitRuleSelector.Select(rules, "7", _vip));
    }

    /// <summary>
    ///     创建每个用户的默认规则
    /// </summary>
    /// <param name="id">规则主键</param>
    /// <param name="limit">请求数上限</param>
    /// <returns>规则快照</returns>
    private static RateLimitRuleSnapshot Common(
        long id
        , int limit
    ) {
        return new RateLimitRuleSnapshot(id, RateLimitAlgorithm.SlidingWindow, RateLimitScope.User, null, null, limit, 60);
    }

    /// <summary>
    ///     创建全局规则
    /// </summary>
    /// <param name="id">规则主键</param>
    /// <param name="limit">请求数上限</param>
    /// <returns>规则快照</returns>
    private static RateLimitRuleSnapshot Global(
        long id
        , int limit
    ) {
        return new RateLimitRuleSnapshot(id, RateLimitAlgorithm.SlidingWindow, RateLimitScope.Global, null, null, limit, 60);
    }

    /// <summary>
    ///     取出规则主键集合
    /// </summary>
    /// <param name="rules">规则集合</param>
    /// <returns>规则主键，保持原顺序</returns>
    private static long[] Ids(IEnumerable<RateLimitRuleSnapshot> rules) {
        return [.. rules.Select(x => x.Id)];
    }

    /// <summary>
    ///     创建角色规则
    /// </summary>
    /// <param name="id">规则主键</param>
    /// <param name="roleCode">角色编码</param>
    /// <param name="limit">请求数上限</param>
    /// <param name="windowSeconds">统计窗口秒数</param>
    /// <returns>规则快照</returns>
    private static RateLimitRuleSnapshot Role(
        long id
        , string roleCode
        , int limit
        , int windowSeconds = 60
    ) {
        return new RateLimitRuleSnapshot(id, RateLimitAlgorithm.SlidingWindow, RateLimitScope.Role, roleCode, null, limit, windowSeconds);
    }

    /// <summary>
    ///     创建指定用户的规则
    /// </summary>
    /// <param name="id">规则主键</param>
    /// <param name="userId">指定的用户主键</param>
    /// <param name="limit">请求数上限</param>
    /// <returns>规则快照</returns>
    private static RateLimitRuleSnapshot SpecificUser(
        long id
        , long userId
        , int limit
    ) {
        return new RateLimitRuleSnapshot(id, RateLimitAlgorithm.SlidingWindow, RateLimitScope.User, null, userId, limit, 60);
    }
}