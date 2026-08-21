namespace AiAdmin.Api.Services;

/// <summary>
///     内存中的接口权限快照
/// </summary>
public sealed record ApiPermissionSnapshot(bool HasApis, IReadOnlySet<string> AnonymousKeys, IReadOnlyDictionary<string, HashSet<string>> ByRole)
{
    /// <summary>
    ///     判断多个角色对接口的权限并集是否包含目标接口
    /// </summary>
    /// <param name="roles">当前用户角色编码</param>
    /// <param name="apiKey">接口键</param>
    /// <returns>拥有权限时返回 true</returns>
    public bool Allows(
        IEnumerable<string> roles
        , string apiKey
    ) {
        return roles.Any(role => ByRole.TryGetValue(role, out var apiKeys) && apiKeys.Contains(apiKey));
    }
}