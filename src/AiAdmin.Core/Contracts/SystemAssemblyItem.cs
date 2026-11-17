namespace AiAdmin.Api.Contracts;

/// <summary>
///     后端已加载程序集的信息
/// </summary>
/// <param name="Name">程序集名称</param>
/// <param name="Version">程序集版本</param>
/// <param name="ProductVersion">产品版本，取自 AssemblyInformationalVersion，不含提交哈希，缺失时为空字符串</param>
/// <param name="Kind">程序集类别：application 为本项目程序集，framework 为运行时与共享框架程序集，package 为 NuGet 包带来的程序集</param>
/// <param name="SizeBytes">程序集文件大小（字节），内存加载或文件不存在时为空</param>
public sealed record SystemAssemblyItem(string Name, string Version, string ProductVersion, string Kind, long? SizeBytes);