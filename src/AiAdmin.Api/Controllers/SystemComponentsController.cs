using System.Reflection;
using System.Runtime.InteropServices;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyModel;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     系统组件控制器，展示后端已加载的程序集和依赖的 NuGet 包
/// </summary>
[ApiController]
[ApiDescription("Open-source components")]
[Route("api/system-components")]
public sealed class SystemComponentsController : ControllerBase
{
    /// <summary>
    ///     本项目程序集的名称前缀
    /// </summary>
    private const string _APPLICATION_PREFIX = "AiAdmin";

    /// <summary>
    ///     运行时和共享框架所在的目录，用于区分框架程序集与 NuGet 包程序集
    /// </summary>
    private static readonly string[] _frameworkDirectories =
    [
        Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty
        , Path.GetDirectoryName(typeof(ControllerBase).Assembly.Location) ?? string.Empty
    ];

    /// <summary>
    ///     查询后端已加载的程序集和 NuGet 包
    /// </summary>
    /// <returns>系统组件查询结果</returns>
    [HttpGet("backend")]
    [ApiDescription("Query backend assemblies and NuGet packages")]
    public ActionResult<ApiResponse<SystemComponentsResult>> Backend() {
        var assemblies = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(x => !x.IsDynamic)
            .Select(ToAssemblyItem)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var packages = (DependencyContext.Default?.RuntimeLibraries ?? [])
            .Where(x => string.Equals(x.Type, "package", StringComparison.OrdinalIgnoreCase))
            .Select(x => new SystemPackageItem(x.Name, x.Version, GetPackageSize(x)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Ok(ApiResponse<SystemComponentsResult>.Ok(new SystemComponentsResult(RuntimeInformation.FrameworkDescription, assemblies, packages)));
    }

    /// <summary>
    ///     读取文件大小
    /// </summary>
    /// <param name="path">文件路径，可为空</param>
    /// <returns>文件字节数，路径为空或文件不存在时返回空</returns>
    private static long? GetFileSize(string? path) {
        if (string.IsNullOrEmpty(path)) {
            return null;
        }

        var file = new FileInfo(path);
        return file.Exists ? file.Length : null;
    }

    /// <summary>
    ///     判断程序集类别
    /// </summary>
    /// <param name="name">程序集名称</param>
    /// <param name="location">程序集文件路径，内存加载时为空</param>
    /// <returns>application、framework 或 package</returns>
    private static string GetKind(
        string? name
        , string location
    ) {
        if (name?.StartsWith(_APPLICATION_PREFIX, StringComparison.Ordinal) == true) {
            return "application";
        }

        var directory = Path.GetDirectoryName(location);
        return directory is not null && _frameworkDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase) ? "framework" : "package";
    }

    /// <summary>
    ///     统计 NuGet 包随应用部署的托管程序集大小之和，已发布的程序集平铺在应用目录，所以按文件名到应用目录查找
    /// </summary>
    /// <param name="library">运行时库描述</param>
    /// <returns>字节数之和，没有可统计的文件时返回空</returns>
    private static long? GetPackageSize(RuntimeLibrary library) {
        var sizes = library
            .RuntimeAssemblyGroups.SelectMany(group => group.AssetPaths)
            .Select(assetPath => GetFileSize(Path.Combine(AppContext.BaseDirectory, Path.GetFileName(assetPath))))
            .Where(size => size.HasValue)
            .Select(size => size!.Value)
            .ToList();
        return sizes.Count == 0 ? null : sizes.Sum();
    }

    /// <summary>
    ///     把程序集转换为展示项
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>程序集展示项</returns>
    private static SystemAssemblyItem ToAssemblyItem(Assembly assembly) {
        var name = assembly.GetName();
        var productVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var plusIndex = productVersion.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0) {
            productVersion = productVersion[..plusIndex];
        }

        return new SystemAssemblyItem(
            name.Name ?? assembly.FullName ?? string.Empty, name.Version?.ToString() ?? string.Empty, productVersion
            , GetKind(name.Name, assembly.Location), GetFileSize(assembly.Location)
        );
    }
}