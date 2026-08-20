// 将客户端动态筛选 JSON 安全转换为 EF Core 可参数化执行的表达式树。

using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Services;

/// <summary>
///     为 EF Core 查询提供 FreeSql 风格的动态筛选能力
/// </summary>
public static class DynamicFilterExtensions
{
    /// <summary>
    ///     对查询应用可递归嵌套的动态筛选条件
    /// </summary>
    /// <typeparam name="T">查询实体类型</typeparam>
    /// <param name="query">待筛选的查询</param>
    /// <param name="filter">动态筛选根节点</param>
    /// <returns>附加筛选条件后的查询</returns>
    public static IQueryable<T> ApplyDynamicFilter<T>(
        this IQueryable<T> query
        , DynamicFilter? filter
    ) {
        if (filter is null) {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "entity");
        var condition = BuildCondition(parameter, filter);
        return condition is null ? query : query.Where(Expression.Lambda<Func<T, bool>>(condition, parameter));
    }

    private static BinaryExpression BuildAny(
        MemberExpression member
        , JsonElement value
        , bool negate
    ) {
        var values = ReadValues(value);
        if (values.Count == 0) {
            throw new DynamicFilterValidationException("Dynamic filter Any value is required.");
        }

        var comparisons = values.Select(item => negate
            ? Expression.NotEqual(member, BuildConstant(member.Type, item))
            : Expression.Equal(member, BuildConstant(member.Type, item))
        );
        return comparisons.Aggregate((
                left
                , right
            ) => negate ? Expression.AndAlso(left, right) : Expression.OrElse(left, right)
        );
    }

    private static BinaryExpression BuildComparison(
        MemberExpression member
        , JsonElement value
        , Func<Expression, Expression, BinaryExpression> factory
    ) {
        var type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        return type != typeof(string) && type != typeof(bool)
            ? factory(member, BuildConstant(member.Type, value))
            : throw new DynamicFilterValidationException("Dynamic filter comparison operator does not support string or boolean fields.");
    }

    private static Expression? BuildCondition(
        ParameterExpression parameter
        , DynamicFilter suppliedFilter
    ) {
        var filter = Unwrap(suppliedFilter);
        var conditions = new List<Expression>();
        if (!string.IsNullOrWhiteSpace(filter.Field) || !string.IsNullOrWhiteSpace(filter.Operator)) {
            conditions.Add(BuildFieldCondition(parameter, filter));
        }

        conditions.AddRange(filter.Filters.Select(child => BuildCondition(parameter, child)).OfType<Expression>());

        if (conditions.Count == 0) {
            return null;
        }

        var useOr = string.Equals(filter.Logic, "Or", StringComparison.OrdinalIgnoreCase);
        return useOr || string.Equals(filter.Logic, "And", StringComparison.OrdinalIgnoreCase)
            ? conditions.Aggregate((
                    left
                    , right
                ) => useOr ? Expression.OrElse(left, right) : Expression.AndAlso(left, right)
            )
            : throw new DynamicFilterValidationException("Dynamic filter logic must be And or Or.");
    }

    private static Expression BuildConstant(
        Type targetType
        , JsonElement value
    ) {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null
                ? Expression.Constant(null, targetType)
                : throw new DynamicFilterValidationException("Dynamic filter value cannot be null for this field.");
        }

        var sourceType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var converted = ReadValue(value, sourceType);
        var constant = Expression.Constant(converted, sourceType);
        return sourceType == targetType ? constant : Expression.Convert(constant, targetType);
    }

    private static Expression BuildFieldCondition(
        ParameterExpression parameter
        , DynamicFilter filter
    ) {
        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Operator)) {
            throw new DynamicFilterValidationException("Dynamic filter field and operator are required.");
        }

        var member = ResolveMember(parameter, filter.Field);
        var operation = filter.Operator.Trim();
        return operation.ToUpperInvariant() switch
        {
            "CONTAINS" => BuildStringCondition(member, filter.Value, nameof(string.Contains), false)
            , "STARTSWITH" => BuildStringCondition(member, filter.Value, nameof(string.StartsWith), false)
            , "ENDSWITH" => BuildStringCondition(member, filter.Value, nameof(string.EndsWith), false)
            , "NOTCONTAINS" => BuildStringCondition(member, filter.Value, nameof(string.Contains), true)
            , "NOTSTARTSWITH" => BuildStringCondition(member, filter.Value, nameof(string.StartsWith), true)
            , "NOTENDSWITH" => BuildStringCondition(member, filter.Value, nameof(string.EndsWith), true)
            , "EQUAL" or "EQUALS" or "EQ" => Expression.Equal(member, BuildConstant(member.Type, filter.Value))
            , "NOTEQUAL" => Expression.NotEqual(member, BuildConstant(member.Type, filter.Value))
            , "GREATERTHAN" => BuildComparison(member, filter.Value, Expression.GreaterThan)
            , "GREATERTHANOREQUAL" => BuildComparison(member, filter.Value, Expression.GreaterThanOrEqual)
            , "LESSTHAN" => BuildComparison(member, filter.Value, Expression.LessThan)
            , "LESSTHANOREQUAL" => BuildComparison(member, filter.Value, Expression.LessThanOrEqual)
            , "RANGE" => BuildRange(member, filter.Value, false)
            , "DATERANGE" => BuildRange(member, filter.Value, true)
            , "ANY" => BuildAny(member, filter.Value, false)
            , "NOTANY" => BuildAny(member, filter.Value, true)
            , "CUSTOM" => throw new DynamicFilterValidationException("Dynamic filter operator Custom is not supported.")
            , _ => throw new DynamicFilterValidationException($"Unsupported dynamic filter operator '{filter.Operator}'.")
        };
    }

    private static BinaryExpression BuildRange(
        MemberExpression member
        , JsonElement value
        , bool dateRange
    ) {
        var values = ReadValues(value);
        if (values.Count != 2) {
            throw new DynamicFilterValidationException("Dynamic filter Range and DateRange require exactly two values.");
        }

        var lower = BuildComparison(member, values[0], Expression.GreaterThanOrEqual);
        var upperValue = dateRange ? GetDateRangeEnd(values[1]) : values[1];
        var upper = BuildComparison(member, upperValue, Expression.LessThan);
        return Expression.AndAlso(lower, upper);
    }

    private static Expression BuildStringCondition(
        MemberExpression member
        , JsonElement value
        , string method
        , bool negate
    ) {
        if (member.Type != typeof(string)) {
            throw new DynamicFilterValidationException($"Dynamic filter operator '{method}' only supports string fields.");
        }

        var text = ReadValue(value, typeof(string)) as string;
        if (string.IsNullOrEmpty(text)) {
            throw new DynamicFilterValidationException("Dynamic filter string value is required.");
        }

        var call = Expression.Call(member, method, Type.EmptyTypes, Expression.Constant(text));
        return negate ? Expression.Not(call) : call;
    }

    private static JsonElement CreateStringElement(string value) {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static JsonElement GetDateRangeEnd(JsonElement value) {
        var text = value.GetString() ?? throw new DynamicFilterValidationException("Dynamic filter DateRange end value is required.");
        var end = text.Length switch
        {
            4 => DateTime.ParseExact(text, "yyyy", CultureInfo.InvariantCulture).AddYears(1)
            , 7 => DateTime.ParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture).AddMonths(1)
            , 10 => DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1)
            , 13 => DateTime.ParseExact(text, "yyyy-MM-dd HH", CultureInfo.InvariantCulture).AddHours(1)
            , 16 => DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).AddMinutes(1)
            , _ => throw new DynamicFilterValidationException("Dynamic filter DateRange end value format is invalid.")
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(end));
        return document.RootElement.Clone();
    }

    private static bool IsCollection(Type type) {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static object? ReadValue(
        JsonElement value
        , Type targetType
    ) {
        try {
            return targetType switch
            {
                _ when targetType == typeof(string) => value.GetString() ?? string.Empty
                , _ when targetType == typeof(DateTimeOffset) => DateTimeOffset.Parse(
                    value.GetString() ?? throw new FormatException(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind
                )
                , _ when targetType == typeof(DateTime) => DateTime.Parse(
                    value.GetString() ?? throw new FormatException(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind
                )
                , _ when targetType.IsEnum => Enum.Parse(targetType, value.GetString() ?? throw new FormatException(), true)
                , _ => JsonSerializer.Deserialize(value.GetRawText(), targetType)
            };
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException or ArgumentException) {
            throw new DynamicFilterValidationException($"Dynamic filter value cannot be converted to {targetType.Name}.");
        }
    }

    private static IReadOnlyList<JsonElement> ReadValues(JsonElement value) {
        return value.ValueKind switch
        {
            JsonValueKind.Array => [.. value.EnumerateArray()]
            , JsonValueKind.String =>
            [
                .. (value.GetString() ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CreateStringElement)
            ]
            , _ => [value]
        };
    }

    private static MemberExpression ResolveMember(
        ParameterExpression parameter
        , string field
    ) {
        Expression current = parameter;
        foreach (var name in field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var property = current.Type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null || property.GetIndexParameters().Length != 0 || IsCollection(property.PropertyType)) {
                throw new DynamicFilterValidationException($"Dynamic filter field '{field}' is not available.");
            }

            current = Expression.Property(current, property);
        }

        return current as MemberExpression ?? throw new DynamicFilterValidationException($"Dynamic filter field '{field}' is not available.");
    }

    private static DynamicFilter Unwrap(DynamicFilter filter) {
        var current = filter;
        while (current.NestedDynamicFilter is not null) {
            current = current.NestedDynamicFilter;
        }

        return current;
    }
}