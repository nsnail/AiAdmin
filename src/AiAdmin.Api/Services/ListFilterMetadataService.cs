// 反射实体列表筛选特性并转换为前端可消费的字段元数据

using System.Reflection;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Services;

/// <summary>
///     列表筛选字段元数据反射服务
/// </summary>
public static class ListFilterMetadataService
{
    /// <summary>
    ///     获取指定实体声明的列表筛选字段
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>筛选字段元数据</returns>
    public static IReadOnlyList<ListFilterFieldResult> GetFields<TEntity>() {
        return
        [
            .. typeof(TEntity)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select((
                        property
                        , index
                    ) => (Property: property, Attribute: property.GetCustomAttribute<ListFilterAttribute>(), DeclarationIndex: index)
                )
                .Where(item => item.Attribute is not null)
                .OrderBy(item => item.Attribute!.Sort)
                .ThenBy(item => item.DeclarationIndex)
                .Select(item => new ListFilterFieldResult(
                        item.Property.Name, item.Attribute!.Label, item.Attribute.Control, item.Attribute.Span, item.Attribute.Sort
                        , item.Attribute.Placeholder
                        , [.. item.Attribute.Options.Select(ToOption)], GetValueType(item.Property.PropertyType)
                    )
                )
        ];
    }

    /// <summary>
    ///     获取前端字段值类型
    /// </summary>
    /// <param name="type">实体属性类型</param>
    /// <returns>前端值类型</returns>
    private static string GetValueType(Type type) {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;
        return valueType switch
        {
            _ when valueType == typeof(bool) => "boolean"
            , _ when valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset) => "date"
            , _ when valueType.IsEnum || valueType.IsPrimitive || valueType == typeof(decimal) => "number"
            , _ => "string"
        };
    }

    /// <summary>
    ///     将特性选项文本转换为响应模型
    /// </summary>
    /// <param name="option">值和显示名称文本</param>
    /// <returns>筛选选项</returns>
    private static ListFilterOptionResult ToOption(string option) {
        var separator = option.IndexOf(':', StringComparison.Ordinal);
        return separator < 0
            ? new ListFilterOptionResult(option, option)
            : new ListFilterOptionResult(option[(separator + 1)..], option[..separator]);
    }
}