using System.Globalization;
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     从接口的全部限流规则里选出对当前请求生效的规则：全局规则始终生效作为保底上限，
///     其余规则按“指定用户 > 角色 > 每个用户的默认规则”取最具体的一层，同一层有多条时只取额度最宽松的一条
/// </summary>
public static class RateLimitRuleSelector
{
    /// <summary>
    ///     选出对当前请求生效的规则
    /// </summary>
    /// <param name="rules">接口上已启用的全部规则</param>
    /// <param name="userId">当前用户主键，未登录时为空</param>
    /// <param name="roleCodes">当前用户拥有的角色编码</param>
    /// <returns>需要依次判定的规则，全局规则在前</returns>
    public static IReadOnlyList<RateLimitRuleSnapshot> Select(
        IReadOnlyList<RateLimitRuleSnapshot> rules
        , string? userId
        , IReadOnlyCollection<string> roleCodes
    ) {
        var selected = rules.Where(x => x.Scope == RateLimitScope.Global).ToList();

        // 指定用户：规则里的用户与当前用户一致才适用，未登录没有用户身份，不适用
        var specific = rules
            .Where(x => x is { Scope: RateLimitScope.User, UserId: not null }
                        && userId is not null
                        && x.UserId.Value.ToString(CultureInfo.InvariantCulture) == userId
            )
            .ToList();

        // 角色：当前用户拥有该角色才适用
        var byRole = rules.Where(x => x is { Scope: RateLimitScope.Role, RoleCode: not null } && roleCodes.Contains(x.RoleCode)).ToList();

        // 每个用户的默认规则：用户范围且没有指定用户，匿名请求同样适用并按客户端地址计数
        var common = rules.Where(x => x is { Scope: RateLimitScope.User, UserId: null }).ToList();

        var layer = new[] { specific, byRole, common }.FirstOrDefault(x => x.Count > 0);
        if (layer is not null) {
            selected.Add(Loosest(layer));
        }

        return selected;
    }

    /// <summary>
    ///     取一组规则里额度最宽松的一条，按每秒允许的请求数比较，相同时取主键较小的保证结果稳定
    /// </summary>
    /// <param name="layer">同一层的规则</param>
    /// <returns>最宽松的规则</returns>
    private static RateLimitRuleSnapshot Loosest(List<RateLimitRuleSnapshot> layer) {
        return layer.OrderByDescending(x => (double)x.Limit / x.WindowSeconds).ThenBy(x => x.Id).First();
    }
}