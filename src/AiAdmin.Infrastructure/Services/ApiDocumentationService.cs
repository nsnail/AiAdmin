using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AiAdmin.Api.Services;

/// <summary>
///     从 MVC 元数据和 XML 注释生成当前用户可访问的接口文档
/// </summary>
/// <param name="actions">操作描述符提供器</param>
/// <param name="permissionCache">接口权限缓存</param>
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
            AddActionDocumentation(action, snapshot, roles, isSuper, xml, groups);
        }

        return BuildResult(groups);
    }

    /// <summary>
    ///     将单个 MVC 操作的可访问接口写入文档分组
    /// </summary>
    /// <param name="action">MVC 操作描述符</param>
    /// <param name="snapshot">接口权限快照</param>
    /// <param name="roles">当前用户角色编码</param>
    /// <param name="isSuper">是否为超级管理员</param>
    /// <param name="xml">XML 文档成员映射</param>
    /// <param name="groups">接口文档分组</param>
    private static void AddActionDocumentation(
        ControllerActionDescriptor action
        , ApiPermissionSnapshot snapshot
        , IReadOnlyCollection<string> roles
        , bool isSuper
        , Dictionary<string, string> xml
        , Dictionary<string, (string Description, List<ApiDocumentationItem> Items)> groups
    ) {
        if (!TryGetDocumentedPath(action, isSuper, out var path)) {
            return;
        }

        foreach (var method in GetHttpMethods(action).Where(method => CanAccess(snapshot, roles, isSuper, method, path))) {
            AddToGroup(groups, action, BuildItem(action, method.ToUpperInvariant(), path, xml), xml);
        }
    }

    /// <summary>
    ///     将接口文档项加入对应控制器分组
    /// </summary>
    /// <param name="groups">接口文档分组</param>
    /// <param name="action">MVC 操作描述符</param>
    /// <param name="item">接口文档项</param>
    /// <param name="xml">XML 文档成员映射</param>
    private static void AddToGroup(
        Dictionary<string, (string Description, List<ApiDocumentationItem> Items)> groups
        , ControllerActionDescriptor action
        , ApiDocumentationItem item
        , Dictionary<string, string> xml
    ) {
        if (!groups.TryGetValue(action.ControllerName, out var group)) {
            var controllerMember = "T:" + action.ControllerTypeInfo.FullName;
            var description = ReadSummary(xml, controllerMember)
                              ?? action.ControllerTypeInfo.GetCustomAttribute<ApiDescriptionAttribute>()?.Description ?? action.ControllerName;
            group = (description, []);
            groups[action.ControllerName] = group;
        }

        group.Items.Add(item);
    }

    /// <summary>
    ///     将单个 XML 成员的摘要和参数说明写入注释字典
    /// </summary>
    /// <param name="member">XML 成员节点</param>
    /// <param name="result">成员注释汇总字典</param>
    private static void AddXmlMemberComments(
        XElement member
        , Dictionary<string, string> result
    ) {
        var key = member.Attribute("name")!.Value;
        result[key] = Clean(member.Element("summary")?.Value);
        foreach (var parameter in member.Elements("param")) {
            var name = parameter.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(name)) {
                result[key + "#" + name] = Clean(parameter.Value);
            }
        }
    }

    /// <summary>
    ///     枚举类型的属性在描述末尾追加全部取值（数字取值与说明），枚举按数字序列化，没有说明的取值只列数字和名称
    /// </summary>
    /// <param name="description">属性原有描述</param>
    /// <param name="propertyType">属性类型，可空枚举同样处理</param>
    /// <param name="xml">XML 文档成员映射</param>
    /// <returns>追加取值后的描述，非枚举类型原样返回</returns>
    private static string AppendEnumValues(
        string description
        , Type propertyType
        , Dictionary<string, string> xml
    ) {
        var actual = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!actual.IsEnum) {
            return description;
        }

        var values = actual
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => Convert.ToInt64(x.GetRawConstantValue(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                         + "="
                         + (ReadSummary(xml, "F:" + actual.FullName + "." + x.Name) ?? x.Name)
            );
        var list = "取值：" + string.Join("；", values);
        return string.IsNullOrEmpty(description) ? list : description + "。" + list;
    }

    /// <summary>
    ///     根据控制器操作和 XML 注释构建接口文档项
    /// </summary>
    /// <param name="action">控制器操作描述符</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="path">接口路径</param>
    /// <param name="xml">XML 文档成员映射</param>
    /// <returns>接口文档项</returns>
    private static ApiDocumentationItem BuildItem(
        ControllerActionDescriptor action
        , string method
        , string path
        , Dictionary<string, string> xml
    ) {
        var member = "M:" + action.MethodInfo.DeclaringType?.FullName + "." + action.MethodInfo.Name;
        var attributeDescription = action.MethodInfo.GetCustomAttribute<ApiDescriptionAttribute>()?.Description;
        var xmlSummary = ReadMethodSummary(xml, member);
        var displayName = xmlSummary ?? attributeDescription ?? action.ActionName;
        var itemDescription = attributeDescription ?? xmlSummary ?? action.ActionName;
        var parameters = new List<ApiDocumentationParameter>();
        ApiDocumentationType? body = null;
        foreach (var parameter in action.Parameters) {
            // 取消令牌由框架注入，不属于接口入参，避免被误判为请求体而覆盖真实请求体
            if (parameter.ParameterType == typeof(CancellationToken)) {
                continue;
            }

            var info = FindParameterInfo(action, parameter.Name);
            var description = ReadParam(xml, member, parameter.Name) ?? string.Empty;
            if (GetParameterSource(parameter, info, path) == "body") {
                body = BuildType(parameter.ParameterType, description, xml);
                continue;
            }

            parameters.Add(BuildParameter(parameter, info, description, path));
        }

        var response = BuildType(UnwrapResponse(action.MethodInfo.ReturnType), string.Empty, xml);
        return new ApiDocumentationItem(
            method, $"/{path.TrimStart('/')}", displayName, itemDescription, action.ControllerName, action.MethodInfo.Name, parameters, body, response
        );
    }

    /// <summary>
    ///     构建非请求体参数文档
    /// </summary>
    /// <param name="parameter">操作参数</param>
    /// <param name="info">反射参数信息</param>
    /// <param name="description">参数说明</param>
    /// <param name="path">接口路径</param>
    /// <returns>参数文档</returns>
    private static ApiDocumentationParameter BuildParameter(
        ParameterDescriptor parameter
        , ParameterInfo? info
        , string description
        , string path
    ) {
        return new ApiDocumentationParameter(
            parameter.Name, GetParameterSource(parameter, info, path), ToType(parameter.ParameterType), info?.IsOptional == false, description
            , info?.DefaultValue?.ToString()
        );
    }

    /// <summary>
    ///     递归构建类型的公开属性文档，自定义类型及其集合会展开内部字段
    /// </summary>
    /// <param name="owner">属性所属类型</param>
    /// <param name="xml">XML 文档成员映射</param>
    /// <param name="path">当前展开链路上的类型，用于避免循环引用无限展开</param>
    /// <returns>属性文档集合</returns>
    private static ApiDocumentationProperty[] BuildProperties(
        Type owner
        , Dictionary<string, string> xml
        , HashSet<Type> path
    ) {
        return
        [
            .. owner
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x =>
                    {
                        var element = GetElementType(x.PropertyType);
                        ApiDocumentationProperty[]? children = null;
                        if (IsCustomClass(element) && !path.Contains(element)) {
                            var next = new HashSet<Type>(path) { element };
                            children = BuildProperties(element, xml, next);
                        }

                        return new ApiDocumentationProperty(
                            x.Name, ToType(x.PropertyType), IsRequiredProperty(x)
                            , AppendEnumValues(ReadSummary(xml, "P:" + owner.FullName + "." + x.Name) ?? string.Empty, x.PropertyType, xml), children
                        );
                    }
                )
        ];
    }

    /// <summary>
    ///     将接口文档分组转换为稳定排序的返回结果
    /// </summary>
    /// <param name="groups">接口文档分组</param>
    /// <returns>接口文档结果</returns>
    private static ApiDocumentationResult BuildResult(Dictionary<string, (string Description, List<ApiDocumentationItem> Items)> groups) {
        return new ApiDocumentationResult(
            [
                .. groups
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new ApiDocumentationGroup(
                            x.Key, x.Value.Description, [.. x.Value.Items.OrderBy(item => item.Path, StringComparer.Ordinal)]
                        )
                    )
            ]
        );
    }

    /// <summary>
    ///     构建类型及其公开属性的接口文档
    /// </summary>
    /// <param name="type">待描述的类型</param>
    /// <param name="description">类型说明</param>
    /// <param name="xml">XML 文档成员映射</param>
    /// <returns>类型文档</returns>
    private static ApiDocumentationType BuildType(
        Type type
        , string description
        , Dictionary<string, string> xml
    ) {
        var actual = type.IsArray ? type.GetElementType()! : type;
        if (actual.IsGenericType && actual.GetGenericTypeDefinition() == typeof(Nullable<>)) {
            actual = actual.GetGenericArguments()[0];
        }

        var properties = IsCustomClass(actual) ? BuildProperties(actual, xml, [actual]) : [];
        return new ApiDocumentationType(actual.Name, ToType(type), description, properties);
    }

    /// <summary>
    ///     判断当前用户是否可以访问指定接口
    /// </summary>
    /// <param name="snapshot">接口权限快照</param>
    /// <param name="roles">当前用户角色编码</param>
    /// <param name="isSuper">是否为超级管理员</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="path">规范化接口路径</param>
    /// <returns>允许访问时返回 true</returns>
    private static bool CanAccess(
        ApiPermissionSnapshot snapshot
        , IReadOnlyCollection<string> roles
        , bool isSuper
        , string method
        , string path
    ) {
        if (isSuper) {
            return true;
        }

        var key = ApiEndpointKey.Create(method, path);
        return snapshot.AnonymousKeys.Contains(key) || snapshot.Allows(roles, key);
    }

    /// <summary>
    ///     清理文档文本中的多余空白字符
    /// </summary>
    /// <param name="value">待清理的文本</param>
    /// <returns>空白规范化后的文本</returns>
    private static string Clean(string? value) {
        return string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     查找操作方法的反射参数信息
    /// </summary>
    /// <param name="action">控制器操作描述符</param>
    /// <param name="name">参数名称</param>
    /// <returns>匹配的反射参数信息</returns>
    private static ParameterInfo? FindParameterInfo(
        ControllerActionDescriptor action
        , string name
    ) {
        return action.MethodInfo.GetParameters().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    ///     获取数组、泛型集合或可空类型的元素类型，其他类型原样返回
    /// </summary>
    /// <param name="type">属性类型</param>
    /// <returns>元素类型</returns>
    private static Type GetElementType(Type type) {
        while (true) {
            if (type.IsArray) {
                type = type.GetElementType()!;
                continue;
            }

            if (Nullable.GetUnderlyingType(type) is { } nullable) {
                type = nullable;
                continue;
            }

            if (type == typeof(string)
                || !type.IsGenericType
                || !typeof(IEnumerable).IsAssignableFrom(type)
                || type.GetGenericArguments().Length != 1) {
                return type;
            }

            type = type.GetGenericArguments()[0];
        }
    }

    /// <summary>
    ///     读取 MVC 操作声明的 HTTP 方法
    /// </summary>
    /// <param name="action">MVC 操作描述符</param>
    /// <returns>去重后的 HTTP 方法集合</returns>
    private static IEnumerable<string> GetHttpMethods(ControllerActionDescriptor action) {
        return action
                   .ActionConstraints?.OfType<HttpMethodActionConstraint>()
                   .SelectMany(x => x.HttpMethods)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
               ?? [];
    }

    /// <summary>
    ///     判断参数来源位置
    /// </summary>
    /// <param name="parameter">操作参数</param>
    /// <param name="info">反射参数信息</param>
    /// <param name="path">接口路径</param>
    /// <returns>参数来源</returns>
    private static string GetParameterSource(
        ParameterDescriptor parameter
        , ParameterInfo? info
        , string path
    ) {
        if (info?.GetCustomAttribute<FromBodyAttribute>() is not null || IsBodyParameter(parameter.ParameterType, info)) {
            return "body";
        }

        if (info?.GetCustomAttribute<FromHeaderAttribute>() is not null) {
            return "header";
        }

        if (info?.GetCustomAttribute<FromRouteAttribute>() is not null
            || path.Contains("{" + parameter.Name + "}", StringComparison.OrdinalIgnoreCase)) {
            return "path";
        }

        return "query";
    }

    /// <summary>
    ///     判断参数是否由 ASP.NET Core 默认绑定到请求体
    /// </summary>
    /// <param name="type">参数类型</param>
    /// <param name="parameter">方法参数信息</param>
    /// <returns>参数为复杂请求体时返回 true</returns>
    private static bool IsBodyParameter(
        Type type
        , ParameterInfo? parameter
    ) {
        if (parameter?.GetCustomAttribute<FromQueryAttribute>() is not null
            || parameter?.GetCustomAttribute<FromRouteAttribute>() is not null
            || parameter?.GetCustomAttribute<FromHeaderAttribute>() is not null) {
            return false;
        }

        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return !actualType.IsPrimitive
               && actualType != typeof(string)
               && actualType != typeof(decimal)
               && actualType != typeof(DateTime)
               && actualType != typeof(DateTimeOffset)
               && actualType != typeof(Guid)
               && !actualType.IsEnum;
    }

    /// <summary>
    ///     判断类型是否为需要展开内部字段的自定义类或记录
    /// </summary>
    /// <param name="type">待判断的类型</param>
    /// <returns>自定义类型时返回 true</returns>
    private static bool IsCustomClass(Type type) {
        return type.IsClass
               && type != typeof(string)
               && type != typeof(object)
               && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;
    }

    /// <summary>
    ///     判断属性是否为必填：声明了 required 关键字、[Required] 或 [JsonRequired] 特性，或为不可空值类型
    /// </summary>
    /// <param name="property">待判断的属性</param>
    /// <returns>必填时返回 true</returns>
    private static bool IsRequiredProperty(PropertyInfo property) {
        return property.GetCustomAttribute<RequiredMemberAttribute>() is not null
               || property.GetCustomAttribute<RequiredAttribute>() is not null
               || property.GetCustomAttribute<JsonRequiredAttribute>() is not null
               || (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null);
    }

    /// <summary>
    ///     根据方法名称前缀读取包含参数签名的方法 XML 注释
    /// </summary>
    /// <param name="xml">XML 文档成员映射</param>
    /// <param name="memberPrefix">不含参数签名的方法成员前缀</param>
    /// <returns>方法摘要文本</returns>
    private static string? ReadMethodSummary(
        Dictionary<string, string> xml
        , string memberPrefix
    ) {
        var entry = xml.FirstOrDefault(x => x.Key.Equals(memberPrefix, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(entry.Value)) {
            entry = xml.FirstOrDefault(x => x.Key.StartsWith(memberPrefix + "(", StringComparison.Ordinal));
        }

        return string.IsNullOrWhiteSpace(entry.Value) ? null : entry.Value;
    }

    /// <summary>
    ///     读取指定方法参数的 XML 文档说明
    /// </summary>
    /// <param name="xml">XML 文档成员映射</param>
    /// <param name="member">方法成员名称前缀</param>
    /// <param name="name">参数名称</param>
    /// <returns>参数说明，不存在时返回 null</returns>
    private static string? ReadParam(
        Dictionary<string, string> xml
        , string member
        , string? name
    ) {
        if (name is null) {
            return null;
        }

        var entry = xml.FirstOrDefault(x =>
            x.Key.StartsWith(member + "(", StringComparison.Ordinal) && x.Key.EndsWith("#" + name, StringComparison.Ordinal)
        );
        return string.IsNullOrWhiteSpace(entry.Value) ? null : entry.Value;
    }

    /// <summary>
    ///     读取指定成员的 XML 文档摘要
    /// </summary>
    /// <param name="xml">XML 文档成员映射</param>
    /// <param name="key">成员键</param>
    /// <returns>成员摘要，不存在时返回 null</returns>
    private static string? ReadSummary(
        Dictionary<string, string> xml
        , string key
    ) {
        return xml.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    /// <summary>
    ///     读取单个程序集 XML 文档中的成员注释
    /// </summary>
    /// <param name="path">XML 文档路径</param>
    /// <param name="result">成员注释汇总字典</param>
    private static void ReadXmlCommentFile(
        string path
        , Dictionary<string, string> result
    ) {
        foreach (var member in XDocument.Load(path).Descendants("member").Where(x => x.Attribute("name") is not null)) {
            AddXmlMemberComments(member, result);
        }
    }

    /// <summary>
    ///     汇总当前应用已加载程序集的 XML 文档注释
    /// </summary>
    /// <returns>XML 文档成员映射</returns>
    private static Dictionary<string, string> ReadXmlComments() {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in AppDomain
                     .CurrentDomain.GetAssemblies()
                     .Select(assembly => Path.ChangeExtension(assembly.Location, ".xml"))
                     .Where(File.Exists)) {
            ReadXmlCommentFile(path, result);
        }

        return result;
    }

    /// <summary>
    ///     将 .NET 类型转换为接口文档使用的类型名称
    /// </summary>
    /// <param name="type">待转换的 .NET 类型</param>
    /// <returns>接口文档类型名称</returns>
    private static string ToType(Type type) {
        if (type.IsArray) {
            return ToType(type.GetElementType()!) + "[]";
        }

        if (Nullable.GetUnderlyingType(type) is { } nullable) {
            return ToType(nullable) + "?";
        }

        if (type == typeof(string) || type == typeof(Guid)) {
            return "string";
        }

        if (type == typeof(bool)) {
            return "boolean";
        }

        if (type == typeof(int)
            || type == typeof(long)
            || type == typeof(short)
            || type == typeof(decimal)
            || type == typeof(double)
            || type == typeof(float)) {
            return "number";
        }

        return type.IsGenericType switch
        {
            true when typeof(IEnumerable).IsAssignableFrom(type) => ToType(type.GetGenericArguments()[0]) + "[]"
            , _ => type.Name
        };
    }

    /// <summary>
    ///     校验操作是否应生成文档并读取规范化路径
    /// </summary>
    /// <param name="action">MVC 操作描述符</param>
    /// <param name="isSuper">是否为超级管理员</param>
    /// <param name="path">规范化接口路径</param>
    /// <returns>操作应生成文档且路由有效时返回 true</returns>
    private static bool TryGetDocumentedPath(
        ControllerActionDescriptor action
        , bool isSuper
        , out string path
    ) {
        path = string.Empty;
        if (!isSuper && action.MethodInfo.GetCustomAttribute<ApiDocumentedAttribute>() is null) {
            return false;
        }

        var template = action.AttributeRouteInfo?.Template;
        if (string.IsNullOrWhiteSpace(template)) {
            return false;
        }

        path = ApiEndpointKey.NormalizePath(template);
        return true;
    }

    /// <summary>
    ///     解包异步、操作结果和统一响应包装类型
    /// </summary>
    /// <param name="type">接口返回类型</param>
    /// <returns>实际响应数据类型</returns>
    private static Type UnwrapResponse(Type type) {
        while (type.IsGenericType
               && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ActionResult<>))) {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>)) {
            type = type.GetGenericArguments()[0];
        }

        return type == typeof(void) ? typeof(object) : type;
    }
}