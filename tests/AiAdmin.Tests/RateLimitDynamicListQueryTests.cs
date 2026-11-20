using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证限流规则列表的动态筛选和排序：字符串、数值、布尔、枚举、集合、嵌套 And/Or、接口路径别名、非法字段和排序稳定性
/// </summary>
public sealed class RateLimitDynamicListQueryTests
{
    private static readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = "ApiEndpoint.Path", ["method"] = "ApiEndpoint.Method"
    };

    private static readonly string[] _roleCodes = ["R_ADMIN", "R_VIP"];

    private static readonly long[] _usersOneAndTwo = [1L, 2L];

    /// <summary>
    ///     接口路径别名映射到关联接口的路径，右键点击接口列可以按路径筛选
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersByApiPathAlias() {
        var filter = Condition("path", "Contains", "login");
        Assert.Equal([1L], Ids(CreateRules().ApplyDynamicFilter(filter, _aliases)));
    }

    /// <summary>
    ///     枚举字段按编号筛选：限流算法和限流范围
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersByEnumFields() {
        Assert.Equal([2L, 3L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.Algorithm), "Equal", 2))));
        Assert.Equal([1L, 3L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.Scope), "Equal", 1))));
    }

    /// <summary>
    ///     按角色编码筛选，右键点击作用对象里的角色标签时使用
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersByRoleCode() {
        Assert.Equal([2L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.RoleCode), "Equal", "R_VIP"))));
    }

    /// <summary>
    ///     按指定用户主键筛选，前端对超出安全整数范围的值以字符串传递，后端必须能接受
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersByUserIdFromString() {
        var filter = Condition(nameof(RateLimitRule.UserId), "Equal", "9007199254740993");
        Assert.Equal([4L], Ids(CreateRules().ApplyDynamicFilter(filter)));
    }

    /// <summary>
    ///     数值、布尔和集合筛选
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersNumericBooleanAndSetFields() {
        Assert.Equal([2L, 3L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.Limit), "GreaterThanOrEqual", 100))));
        Assert.Equal([4L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.IsEnabled), "Equal", false))));
        Assert.Equal([1L, 2L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.UserId), "Any", _usersOneAndTwo))));
        Assert.Equal(
            [2L], Ids(CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.RoleCode), "Any", _roleCodes)).Where(x => x.Limit == 100))
        );
    }

    /// <summary>
    ///     没有别名时不能用 path 字段，后端必须报错而不是忽略条件
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_RejectsUnknownFieldWithoutAlias() {
        var filter = Condition("path", "Contains", "login");
        _ = Assert.Throws<DynamicFilterValidationException>(() => _ = CreateRules().ApplyDynamicFilter(filter).ToList());
    }

    /// <summary>
    ///     嵌套 And/Or：启用且（滑动窗口或者备注包含 vip）
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_SupportsNestedGroups() {
        var filter = new DynamicFilter
        {
            Logic = "And"
            , Filters =
            [
                Condition(nameof(RateLimitRule.IsEnabled), "Equal", true)
                , new DynamicFilter
                {
                    Logic = "Or"
                    , Filters =
                    [
                        Condition(nameof(RateLimitRule.Algorithm), "Equal", 1)
                        , Condition(nameof(RateLimitRule.Remark), "Contains", "vip")
                    ]
                }
            ]
        };
        Assert.Equal([1L, 2L], Ids(CreateRules().ApplyDynamicFilter(filter)));
    }

    /// <summary>
    ///     接口列排序按关联接口的路径，别名映射后不报错
    /// </summary>
    [Fact]
    public void ApplyDynamicSort_SortsByApiPathAlias() {
        var sorted = CreateRules().ApplyDynamicSort("path", "asc", nameof(RateLimitRule.CreatedAt), true, _aliases);
        Assert.Equal([3L, 1L, 2L, 4L], Ids(sorted));
    }

    /// <summary>
    ///     排序字段取值相同时用主键作次级排序，分页结果稳定且不重复
    /// </summary>
    [Fact]
    public void ApplyDynamicSort_WithKeyTieBreakerPagesStably() {
        var ordered = (IOrderedQueryable<RateLimitRule>)CreateRules()
            .ApplyDynamicSort(nameof(RateLimitRule.Scope), "asc", nameof(RateLimitRule.CreatedAt), true);
        var sorted = ordered.ThenByKey(x => x.Id, nameof(RateLimitRule.Scope), "asc", true);
        var pages = sorted.Skip(0).Take(2).Concat(sorted.Skip(2).Take(2)).Select(x => x.Id).ToList();

        // 限流范围升序：全局(1、3)、用户(4)、角色(2)；同范围按主键升序
        Assert.Equal([1L, 3L, 4L, 2L], pages);
        Assert.Equal(pages.Count, pages.Distinct().Count());
    }

    /// <summary>
    ///     非法操作符和非法排序字段必须返回明确错误
    /// </summary>
    [Fact]
    public void Dynamic_RejectsInvalidOperatorAndSortField() {
        _ = Assert.Throws<DynamicFilterValidationException>(() =>
            _ = CreateRules().ApplyDynamicFilter(Condition(nameof(RateLimitRule.Limit), "Explode", 1)).ToList()
        );
        _ = Assert.Throws<DynamicFilterValidationException>(() =>
            _ = CreateRules().ApplyDynamicSort("nope", "asc", nameof(RateLimitRule.CreatedAt), true).ToList()
        );
    }

    /// <summary>
    ///     创建动态筛选条件
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="operation">操作符</param>
    /// <param name="value">条件值</param>
    /// <returns>动态筛选条件</returns>
    private static DynamicFilter Condition(
        string field
        , string operation
        , object value
    ) {
        return new DynamicFilter { Field = field, Operator = operation, Value = JsonSerializer.SerializeToElement(value) };
    }

    /// <summary>
    ///     创建测试用限流规则
    /// </summary>
    /// <returns>限流规则查询</returns>
    private static IQueryable<RateLimitRule> CreateRules() {
        var login = new ApiEndpoint { Method = "POST", Path = "/api/auth/login", Name = "Login" };
        var roleList = new ApiEndpoint { Method = "POST", Path = "/api/role/list", Name = "Role list" };
        var apiAdmin = new ApiEndpoint { Method = "GET", Path = "/api/admin/info", Name = "Admin info" };
        return new[]
        {
            new RateLimitRule
            {
                Id = 1L
                , ApiEndpoint = login
                , Algorithm = RateLimitAlgorithm.SlidingWindow
                , Scope = RateLimitScope.Global
                , Limit = 10
                , WindowSeconds = 60
                , IsEnabled = true
                , UserId = 1L
                , Remark = "login guard"
                , CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
            , new RateLimitRule
            {
                Id = 2L
                , ApiEndpoint = roleList
                , Algorithm = RateLimitAlgorithm.LeakyBucket
                , Scope = RateLimitScope.Role
                , RoleCode = "R_VIP"
                , Limit = 100
                , WindowSeconds = 60
                , IsEnabled = true
                , UserId = 2L
                , Remark = "vip relax"
                , CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            }
            , new RateLimitRule
            {
                Id = 3L
                , ApiEndpoint = apiAdmin
                , Algorithm = RateLimitAlgorithm.LeakyBucket
                , Scope = RateLimitScope.Global
                , Limit = 500
                , WindowSeconds = 10
                , IsEnabled = true
                , Remark = "admin"
                , CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            }
            , new RateLimitRule
            {
                Id = 4L
                , ApiEndpoint = roleList
                , Algorithm = RateLimitAlgorithm.TokenBucket
                , Scope = RateLimitScope.User
                , Limit = 5
                , WindowSeconds = 1
                , IsEnabled = false
                , UserId = 9007199254740993L
                , Remark = "per user"
                , CreatedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)
            }
        }.AsQueryable();
    }

    /// <summary>
    ///     取出规则主键集合
    /// </summary>
    /// <param name="rules">规则查询</param>
    /// <returns>规则主键，保持查询顺序</returns>
    private static long[] Ids(IQueryable<RateLimitRule> rules) {
        return [.. rules.Select(x => x.Id)];
    }
}