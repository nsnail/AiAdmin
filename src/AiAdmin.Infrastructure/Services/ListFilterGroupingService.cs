using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AiAdmin.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     根据列表筛选元数据生成字段分组计数
/// </summary>
public static class ListFilterGroupingService
{
    private static readonly MethodInfo _getOptionsMethod = typeof(ListFilterGroupingService).GetMethod(
        nameof(GetOptionsAsync), BindingFlags.Public | BindingFlags.Static
    )!;

    /// <summary>
    ///     按标记为分组计数的实体属性统计当前查询数据
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">未应用动态筛选的实体查询</param>
    /// <param name="dynamicFilter">当前动态筛选条件</param>
    /// <param name="aliases">对外筛选字段与实体路径的别名</param>
    /// <returns>字段分组统计结果</returns>
    /// <exception cref="InvalidOperationException">分组字段不存在时引发</exception>
    public static async Task<IReadOnlyList<ListFilterGroupResult>> GetGroupsAsync<TEntity>(
        IQueryable<TEntity> source
        , DynamicFilter? dynamicFilter
        , IReadOnlyDictionary<string, string>? aliases = null
    )
        where TEntity : class {
        var fields = ListFilterMetadataService.GetFields<TEntity>().Where(x => x.GroupCount).ToArray();
        var results = new List<ListFilterGroupResult>(fields.Length);
        foreach (var field in fields) {
            var property = typeof(TEntity).GetProperty(field.Field, BindingFlags.Instance | BindingFlags.Public)
                           ?? throw new InvalidOperationException($"List filter group field '{field.Field}' does not exist");
            var query = source.ApplyDynamicFilter(RemoveField(dynamicFilter, field.Field), aliases);
            var method = _getOptionsMethod.MakeGenericMethod(typeof(TEntity), property.PropertyType);
            var task = (Task<(int Total, IReadOnlyList<ListFilterGroupOptionResult> Options)>)method.Invoke(
                null, [query, property, field]
            )!;
            var (total, options) = await task.ConfigureAwait(false);
            results.Add(new ListFilterGroupResult(field.Field, field.Label, field.ValueType, total, options));
        }

        return results;
    }

    /// <summary>
    ///     查询单个属性的分组值和数量
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TValue">属性值类型</typeparam>
    /// <param name="source">已应用其他字段筛选的实体查询</param>
    /// <param name="property">分组属性</param>
    /// <param name="metadata">字段筛选元数据</param>
    /// <returns>分组总量和选项集合</returns>
    public static async Task<(int Total, IReadOnlyList<ListFilterGroupOptionResult> Options)> GetOptionsAsync<TEntity, TValue>(
        IQueryable<TEntity> source
        , PropertyInfo property
        , ListFilterFieldResult metadata
    )
        where TEntity : class {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var selector = Expression.Lambda<Func<TEntity, TValue>>(Expression.Property(parameter, property), parameter);
        var rows = await source
            .GroupBy(selector)
            .Select(group => new KeyValuePair<TValue, int>(group.Key, group.Count()))
            .ToListAsync()
            .ConfigureAwait(false);
        var counts = rows.ToDictionary(row => ToLookupKey(row.Key, typeof(TValue)), row => row.Value, StringComparer.OrdinalIgnoreCase);
        var options = new List<ListFilterGroupOptionResult>();
        foreach (var option in metadata.Options) {
            _ = counts.Remove(option.Value, out var count);
            options.Add(new ListFilterGroupOptionResult(ConvertOptionValue(option.Value, typeof(TValue)), option.Label, count));
        }

        foreach (var row in rows.Where(row => counts.ContainsKey(ToLookupKey(row.Key, typeof(TValue))))) {
            var value = NormalizeValue(row.Key, typeof(TValue));
            options.Add(new ListFilterGroupOptionResult(value, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, row.Value));
        }

        return (rows.Sum(row => row.Value), options);
    }

    /// <summary>
    ///     从动态筛选树中移除指定字段条件
    /// </summary>
    /// <param name="filter">动态筛选节点</param>
    /// <param name="field">需要移除的字段</param>
    /// <returns>移除字段后的动态筛选节点</returns>
    private static DynamicFilter? RemoveField(DynamicFilter? filter, string field) {
        if (filter is null || string.Equals(filter.Field, field, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var nested = RemoveField(filter.NestedDynamicFilter, field);
        var children = filter.Filters.Select(child => RemoveField(child, field)).Where(child => child is not null).Cast<DynamicFilter>().ToList();
        return filter.Field is null && nested is null && children.Count == 0
            ? null
            : new DynamicFilter
            {
                Field = filter.Field
                , Operator = filter.Operator
                , Value = filter.Value
                , Logic = filter.Logic
                , NestedDynamicFilter = nested
                , Filters = children
            };
    }

    /// <summary>
    ///     将元数据选项文本转换为实体属性值
    /// </summary>
    /// <param name="value">元数据选项值</param>
    /// <param name="type">实体属性类型</param>
    /// <returns>可作为动态筛选值的属性值</returns>
    private static object? ConvertOptionValue(string value, Type type) {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        if (targetType.IsEnum) {
            var underlyingValue = Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
            return Convert.ChangeType(underlyingValue, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     将数据库分组值转换为稳定的查询值
    /// </summary>
    /// <param name="value">数据库分组值</param>
    /// <param name="type">实体属性类型</param>
    /// <returns>可序列化的查询值</returns>
    private static object? NormalizeValue(object? value, Type type) {
        if (value is null) {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsEnum ? Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture) : value;
    }

    /// <summary>
    ///     将分组值转换为元数据选项匹配键
    /// </summary>
    /// <param name="value">分组值</param>
    /// <param name="type">实体属性类型</param>
    /// <returns>不受区域设置影响的匹配键</returns>
    private static string ToLookupKey(object? value, Type type) {
        return Convert.ToString(NormalizeValue(value, type), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}