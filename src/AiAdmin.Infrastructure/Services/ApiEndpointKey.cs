// 生成统一的请求方法与路径键，确保同步和鉴权使用同一标识。
namespace AiAdmin.Api.Services;

/// <summary>
///     提供接口方法与路径的规范化标识
/// </summary>
public static class ApiEndpointKey
{
    /// <summary>
    ///     生成接口唯一键
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="path">接口路径</param>
    /// <returns>规范化接口键</returns>
    public static string Create(
        string method
        , string path
    ) {
        return $"{method.Trim().ToUpperInvariant()} {NormalizePath(path)}";
    }

    /// <summary>
    ///     规范化接口路径
    /// </summary>
    /// <param name="path">原始接口路径</param>
    /// <returns>以斜杠开头的小写路径</returns>
    public static string NormalizePath(string path) {
        // 去除首尾斜杠并统一为小写，避免路径格式差异造成权限 miss。
        var normalized = "/" + path.Trim().Trim('/');
        return normalized == "/" ? normalized : normalized.ToLowerInvariant();
    }
}