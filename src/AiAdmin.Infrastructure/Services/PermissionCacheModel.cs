// 定义分布式缓存使用的接口权限序列化模型
namespace AiAdmin.Api.Services;

/// <summary>
///     分布式缓存中的接口权限序列化模型
/// </summary>
/// <param name="HasApis">是否存在接口配置</param>
/// <param name="AnonymousKeys">允许匿名访问的接口键集合</param>
/// <param name="ByRole">按角色编码分组的接口键集合</param>
internal sealed record PermissionCacheModel(bool HasApis, string[] AnonymousKeys, Dictionary<string, string[]> ByRole);