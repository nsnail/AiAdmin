namespace AiAdmin.Api.Contracts;

/// <summary>
///     后端依赖的 NuGet 包信息，包含直接引用和传递引用的包
/// </summary>
/// <param name="Name">包名称</param>
/// <param name="Version">包版本</param>
/// <param name="SizeBytes">包随应用部署的托管程序集文件大小之和（字节），没有可统计的文件时为空</param>
public sealed record SystemPackageItem(string Name, string Version, long? SizeBytes);