namespace AiAdmin.Api.Contracts;

/// <summary>
///     系统组件查询结果，包含后端运行时、已加载程序集和 NuGet 包
/// </summary>
/// <param name="RuntimeVersion">后端运行时描述，例如 .NET 11.0.0</param>
/// <param name="Assemblies">已加载程序集，按名称升序</param>
/// <param name="Packages">NuGet 包，按名称升序</param>
public sealed record SystemComponentsResult(
    string RuntimeVersion
    , IReadOnlyList<SystemAssemblyItem> Assemblies
    , IReadOnlyList<SystemPackageItem> Packages);