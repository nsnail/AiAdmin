using AiAdmin.Api.Contracts;
using AiAdmin.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证系统组件接口：返回已加载程序集与 NuGet 包，且都带版本号
/// </summary>
public sealed class SystemComponentsControllerTests
{
    /// <summary>
    ///     验证程序集包含本项目程序集且标记为 application，列表按名称升序且版本号不为空
    /// </summary>
    [Fact]
    public void Backend_ReturnsLoadedAssembliesWithVersions() {
        var data = Query();

        Assert.False(string.IsNullOrWhiteSpace(data.RuntimeVersion));
        var api = Assert.Single(data.Assemblies, x => x.Name == "AiAdmin.Api");
        Assert.Equal("application", api.Kind);
        Assert.All(data.Assemblies, x => Assert.False(string.IsNullOrWhiteSpace(x.Version)));
        Assert.Equal(data.Assemblies.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), data.Assemblies.Select(x => x.Name));
        Assert.Contains(data.Assemblies, x => x is { Name: "System.Private.CoreLib", Kind: "framework" });
        Assert.True(api.SizeBytes > 0);
    }

    /// <summary>
    ///     验证 NuGet 包列表包含项目直接引用的包，版本号不为空且按名称升序
    /// </summary>
    [Fact]
    public void Backend_ReturnsNuGetPackagesWithVersions() {
        var data = Query();

        Assert.NotEmpty(data.Packages);
        var redis = Assert.Single(data.Packages, x => x.Name == "StackExchange.Redis");
        Assert.True(redis.SizeBytes > 0);
        Assert.All(data.Packages, x => Assert.False(string.IsNullOrWhiteSpace(x.Version)));
        Assert.Equal(data.Packages.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), data.Packages.Select(x => x.Name));
    }

    /// <summary>
    ///     调用接口并取出结果
    /// </summary>
    /// <returns>系统组件查询结果</returns>
    private static SystemComponentsResult Query() {
        var result = new SystemComponentsController().Backend();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<SystemComponentsResult>>(ok.Value, false).Data!;
    }
}