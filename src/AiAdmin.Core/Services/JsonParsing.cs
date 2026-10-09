using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiAdmin.Api.Services;

/// <summary>
///     提供统一的 JSON 属性名大小写规则及文档字段读取能力
/// </summary>
public static class JsonParsing
{
    /// <summary>
    ///     只忽略属性名大小写的只读反序列化选项，保留默认值类型校验
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>
    ///     使可变 JSON 对象及其嵌套对象按忽略大小写方式读取属性
    /// </summary>
    public static JsonNodeOptions NodeOptions { get; } = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     获取忽略大小写的必需属性，缺失时明确报错
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>对应属性值</returns>
    /// <exception cref="KeyNotFoundException">JSON 对象缺少指定属性</exception>
    public static JsonElement GetPropertyIgnoreCase(this JsonElement element, string propertyName) {
        return element.TryGetPropertyIgnoreCase(propertyName, out var value)
            ? value
            : throw new KeyNotFoundException($"JSON property '{propertyName}' was not found.");
    }

    /// <summary>
    ///     按序号规则忽略属性名大小写，重复属性采用最后一个值
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">找到的属性值</param>
    /// <returns>是否找到指定属性</returns>
    public static bool TryGetPropertyIgnoreCase(this JsonElement element, string propertyName, out JsonElement value) {
        ArgumentNullException.ThrowIfNull(propertyName);
        value = default;
        var found = false;
        foreach (var property in element.EnumerateObject().Where(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))) {
            value = property.Value;
            found = true;
        }

        return found;
    }

    /// <summary>
    ///     创建并冻结共享选项，避免运行时修改影响其他解析入口
    /// </summary>
    /// <returns>统一的只读反序列化选项</returns>
    private static JsonSerializerOptions CreateOptions() {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}