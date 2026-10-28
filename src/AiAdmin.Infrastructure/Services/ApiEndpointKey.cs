using AiAdmin.Api.Data;

namespace AiAdmin.Api.Services;

/// <summary>
///     提供接口方法与路径的规范化标识
/// </summary>
public static class ApiEndpointKey
{
    /// <summary>
    ///     计算接口路径的 CRC32，路径先规范化再计算，请求日志与接口表使用同一规则，便于用日志中的整数还原接口
    /// </summary>
    /// <param name="path">接口路径，可以带查询字符串，查询字符串会被忽略</param>
    /// <returns>规范化路径的有符号 CRC32</returns>
    public static int ComputePathCrc32(string path) {
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        return Crc32.Compute(NormalizePath(queryIndex >= 0 ? path[..queryIndex] : path));
    }

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