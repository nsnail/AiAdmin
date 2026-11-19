using System.Reflection;
using AiAdmin.Api.Controllers;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证默认授予角色的接口键表与真实控制器接口保持一致，避免接口改名或改方法后权限静默失效
/// </summary>
public sealed class BasicApiKeysTests
{
    /// <summary>
    ///     验证后台管理类接口不会被默认授予所有角色，避免普通用户越权群发消息或管理用户
    /// </summary>
    [Fact]
    public void BasicApiKeys_DoNotIncludeAdministrationEndpoints() {
        string[] forbiddenPrefixes =
        [
            "POST /api/message", "GET /api/message", "POST /api/user/list", "POST /api/user/update", "POST /api/user/enabled"
            , "POST /api/support/reply"
        ];

        var leaked = DatabaseInitializer
            .BasicApiKeys.Where(key => forbiddenPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(leaked);
    }

    /// <summary>
    ///     验证基础接口和客服工作台接口键都对应真实存在的接口
    /// </summary>
    [Fact]
    public void DefaultGrantedKeys_AllMatchExistingEndpoints() {
        var actual = CollectEndpointKeys();

        var missing = DatabaseInitializer.BasicApiKeys.Concat(DatabaseInitializer.SupportServiceApiKeys).Where(x => !actual.Contains(x)).ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    ///     验证默认授予的接口只使用 GET 或 POST 方法
    /// </summary>
    [Fact]
    public void DefaultGrantedKeys_OnlyUseGetOrPost() {
        var invalid = DatabaseInitializer
            .BasicApiKeys.Concat(DatabaseInitializer.SupportServiceApiKeys)
            .Where(x => !x.StartsWith("GET ", StringComparison.Ordinal) && !x.StartsWith("POST ", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(invalid);
    }

    /// <summary>
    ///     通过控制器类型上的路由和方法上的 HTTP 特性收集全部真实接口键，规则与接口同步服务一致
    /// </summary>
    /// <returns>接口键集合</returns>
    private static HashSet<string> CollectEndpointKeys() {
        var controllers = typeof(SupportChatController).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(ControllerBase)) && !x.IsAbstract);
        return [with(StringComparer.Ordinal), .. controllers.SelectMany(ReadEndpointKeys)];
    }

    /// <summary>
    ///     收集单个控制器上全部带 HTTP 方法特性的接口键，没有路由模板的控制器不产生接口
    /// </summary>
    /// <param name="controller">控制器类型</param>
    /// <returns>该控制器的接口键</returns>
    private static IEnumerable<string> ReadEndpointKeys(Type controller) {
        var routeTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template;
        return string.IsNullOrWhiteSpace(routeTemplate)
            ? []
            : controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
                .SelectMany(attribute => attribute.HttpMethods.Select(httpMethod => ApiEndpointKey.Create(
                            httpMethod, string.IsNullOrEmpty(attribute.Template) ? routeTemplate : $"{routeTemplate}/{attribute.Template}"
                        )
                    )
                );
    }
}