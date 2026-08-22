using System.Reflection;
using System.Security.Claims;
using System.Xml.Linq;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
#pragma warning disable IDE0011, IDE0022, IDE0046, RCS1146, RCS1238, S1075, S3358, SA1503, SA1513, SA1516

namespace AiAdmin.Api.Services;

/// <summary>
///     从 MVC 元数据和 XML 注释生成当前用户可访问的接口文档
/// </summary>
public sealed class ApiDocumentationService(IActionDescriptorCollectionProvider actions, ApiPermissionCache permissionCache)
{
    /// <summary>
    ///     读取当前用户有权限的接口文档
    /// </summary>
    /// <param name="user">当前用户</param>
    /// <returns>按控制器分组的接口文档</returns>
    public async Task<ApiDocumentationResult> GetAsync(ClaimsPrincipal user) {
        var snapshot = await permissionCache.GetAsync().ConfigureAwait(false);
        var roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var isSuper = roles.Contains("R_SUPER", StringComparer.Ordinal);
        var xml = ReadXmlComments();
        var groups = new Dictionary<string, (string Description, List<ApiDocumentationItem> Items)>(StringComparer.Ordinal);

        foreach (var action in actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()) {
            if (!isSuper && action.MethodInfo.GetCustomAttribute<ApiDocumentedAttribute>() is null) continue;
            var template = action.AttributeRouteInfo?.Template;
            if (string.IsNullOrWhiteSpace(template)) continue;
            var methods = action.ActionConstraints?.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
                              .SelectMany(x => x.HttpMethods).Distinct(StringComparer.OrdinalIgnoreCase) ?? [];
            foreach (var method in methods) {
                var path = ApiEndpointKey.NormalizePath(template);
                if (!isSuper && !snapshot.AnonymousKeys.Contains(ApiEndpointKey.Create(method, path)) && !snapshot.Allows(roles, ApiEndpointKey.Create(method, path))) continue;
                var item = BuildItem(action, method.ToUpperInvariant(), path, xml);
                if (!groups.TryGetValue(action.ControllerName, out var group)) {
                    group = (ReadSummary(xml, "T:" + action.ControllerTypeInfo.FullName) ?? action.ControllerName, []);
                    groups[action.ControllerName] = group;
                }
                group.Items.Add(item);
            }
        }

        return new ApiDocumentationResult(groups.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new ApiDocumentationGroup(x.Key, x.Value.Description, x.Value.Items.OrderBy(i => i.Path, StringComparer.Ordinal).ToArray())).ToArray());
    }

    private static ApiDocumentationItem BuildItem(ControllerActionDescriptor action, string method, string path, Dictionary<string, string> xml) {
        var member = "M:" + action.MethodInfo.DeclaringType?.FullName + "." + action.MethodInfo.Name;
        var parameters = new List<ApiDocumentationParameter>();
        ApiDocumentationType? body = null;
        foreach (var parameter in action.Parameters) {
            var info = action.MethodInfo.GetParameters().FirstOrDefault(x => string.Equals(x.Name, parameter.Name, StringComparison.Ordinal));
            var source = info?.GetCustomAttribute<FromBodyAttribute>() is not null ? "body" : info?.GetCustomAttribute<FromHeaderAttribute>() is not null ? "header" : info?.GetCustomAttribute<FromRouteAttribute>() is not null || path.Contains("{" + parameter.Name + "}", StringComparison.OrdinalIgnoreCase) ? "path" : "query";
            var type = ToType(parameter.ParameterType);
            var description = ReadParam(xml, member, parameter.Name) ?? string.Empty;
            if (source == "body") body = BuildType(parameter.ParameterType, description, xml);
            else parameters.Add(new ApiDocumentationParameter(parameter.Name ?? "parameter", source, type, info is not null && !info.IsOptional, description, info?.DefaultValue?.ToString()));
        }
        var response = BuildType(UnwrapResponse(action.MethodInfo.ReturnType), string.Empty, xml);
        return new ApiDocumentationItem(method, "/" + path.TrimStart('/'), ReadSummary(xml, member) ?? action.ActionName, ReadSummary(xml, member) ?? string.Empty, action.ControllerName, action.MethodInfo.Name, parameters, body, response);
    }

    private static Type UnwrapResponse(Type type) {
        while (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ActionResult<>))) type = type.GetGenericArguments()[0];
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>)) type = type.GetGenericArguments()[0];
        return type == typeof(void) ? typeof(object) : type;
    }

    private static ApiDocumentationType BuildType(Type type, string description, Dictionary<string, string> xml) {
        var actual = type.IsArray ? type.GetElementType()! : type;
        if (actual.IsGenericType && actual.GetGenericTypeDefinition() == typeof(Nullable<>)) actual = actual.GetGenericArguments()[0];
        var properties = actual.IsClass && actual != typeof(string) ? actual.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => new ApiDocumentationProperty(x.Name, ToType(x.PropertyType), x.PropertyType.IsValueType && Nullable.GetUnderlyingType(x.PropertyType) is null, ReadSummary(xml, "P:" + actual.FullName + "." + x.Name) ?? string.Empty)).ToArray() : [];
        return new ApiDocumentationType(actual.Name, ToType(type), description, properties);
    }

    private static string ToType(Type type) {
        if (type.IsArray) return ToType(type.GetElementType()!) + "[]";
        if (Nullable.GetUnderlyingType(type) is { } nullable) return ToType(nullable) + "?";
        if (type == typeof(string) || type == typeof(Guid)) return "string";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "number";
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return ToType(type.GetGenericArguments()[0]) + "[]";
        return type.Name;
    }

    private static Dictionary<string, string> ReadXmlComments() {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            var path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path)) continue;
            foreach (var member in XDocument.Load(path).Descendants("member").Where(x => x.Attribute("name") is not null)) {
                var key = member.Attribute("name")!.Value;
                result[key] = Clean(member.Element("summary")?.Value);
                foreach (var parameter in member.Elements("param")) {
                    var name = parameter.Attribute("name")?.Value;
                    if (!string.IsNullOrWhiteSpace(name)) result[key + "#" + name] = Clean(parameter.Value);
                }
            }
        }
        return result;
    }

    private static string Clean(string? value) => string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string? ReadSummary(Dictionary<string, string> xml, string key) => xml.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    private static string? ReadParam(Dictionary<string, string> xml, string member, string? name) {
        if (name is null) return null;
        var entry = xml.FirstOrDefault(x => x.Key.StartsWith(member + "(", StringComparison.Ordinal) && x.Key.EndsWith("#" + name, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(entry.Value) ? null : entry.Value;
    }
}