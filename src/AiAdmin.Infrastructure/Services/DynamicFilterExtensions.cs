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
    /// <param name="aliases">客户端字段名到实体字段路径的映射</param>
    /// <returns>附加筛选条件后的查询</returns>
    public static IQueryable<T> ApplyDynamicFilter<T>(
        this IQueryable<T> query
        , DynamicFilter? filter
        , IReadOnlyDictionary<string, string>? aliases = null
    ) {
        if (filter is null) {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "entity");
        var condition = BuildCondition(parameter, filter, aliases);
        return condition is null ? query : query.Where(Expression.Lambda<Func<T, bool>>(condition, parameter));
    }

    /// <summary>
    ///     根据客户端字段名应用动态排序
    /// </summary>
    /// <typeparam name="T">查询实体类型</typeparam>
    /// <param name="query">待排序的查询</param>
    /// <param name="sortField">排序字段名称</param>
    /// <param name="sortOrder">排序方向</param>
    /// <param name="defaultField">未指定排序字段时使用的默认字段</param>
    /// <param name="defaultDescending">默认字段是否倒序</param>
    /// <param name="aliases">客户端字段名到实体字段路径的映射</param>
    /// <returns>附加排序后的查询</returns>
    /// <exception cref="DynamicFilterValidationException">排序方向或字段无效时抛出</exception>
    public static IQueryable<T> ApplyDynamicSort<T>(
        this IQueryable<T> query
        , string? sortField
        , string? sortOrder
        , string defaultField
        , bool defaultDescending = false
        , IReadOnlyDictionary<string, string>? aliases = null
    ) {
        var suppliedField = sortField?.Trim();
        var field = string.IsNullOrWhiteSpace(suppliedField) ? defaultField : suppliedField;
        if (aliases is not null && aliases.TryGetValue(field, out var mappedField)) {
            field = mappedField;
        }

        var descending = string.IsNullOrWhiteSpace(suppliedField)
            ? defaultDescending
            : string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(sortOrder)
            && !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase)) {
            throw new DynamicFilterValidationException("Dynamic sort order must be asc or desc.");
        }

        var parameter = Expression.Parameter(typeof(T), "entity");
        Expression member = parameter;
        foreach (var segment in field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var property = member.Type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                           ?? throw new DynamicFilterValidationException($"Dynamic sort field '{field}' is not available.");
            member = Expression.Property(member, property);
        }

        var selector = Expression.Lambda(member, parameter);
        var method = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var ordered = Expression.Call(typeof(Queryable), method, [typeof(T), member.Type], query.Expression, Expression.Quote(selector));
        return query.Provider.CreateQuery<T>(ordered);
    }

    /// <summary>
    ///     构建 BuildAny 方法对应的业务数据
    /// </summary>
    /// <param name="member">成员名称</param>
    /// <param name="value">待处理的值</param>
    /// <param name="negate">方法参数 negate</param>
    /// <returns>BuildAny 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">集合筛选值为空时抛出</exception>
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

    /// <summary>
    ///     构建 BuildComparison 方法对应的业务数据
    /// </summary>
    /// <param name="member">成员名称</param>
    /// <param name="value">待处理的值</param>
    /// <param name="factory">方法参数 factory</param>
    /// <returns>BuildComparison 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">字段类型不支持比较操作时抛出</exception>
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

    /// <summary>
    ///     构建 BuildCondition 方法对应的业务数据
    /// </summary>
    /// <param name="parameter">参数信息</param>
    /// <param name="suppliedFilter">方法参数 suppliedFilter</param>
    /// <param name="aliases">方法参数 aliases</param>
    /// <returns>BuildCondition 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">筛选逻辑无效时抛出</exception>
    private static Expression? BuildCondition(
        ParameterExpression parameter
        , DynamicFilter suppliedFilter
        , IReadOnlyDictionary<string, string>? aliases
    ) {
        var filter = Unwrap(suppliedFilter);
        var conditions = new List<Expression>();
        if (!string.IsNullOrWhiteSpace(filter.Field) || !string.IsNullOrWhiteSpace(filter.Operator)) {
            conditions.Add(BuildFieldCondition(parameter, filter, aliases));
        }

        conditions.AddRange(filter.Filters.Select(child => BuildCondition(parameter, child, aliases)).OfType<Expression>());

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

    /// <summary>
    ///     构建 BuildConstant 方法对应的业务数据
    /// </summary>
    /// <param name="targetType">方法参数 targetType</param>
    /// <param name="value">待处理的值</param>
    /// <returns>BuildConstant 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">非空值类型接收到空值时抛出</exception>
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

    /// <summary>
    ///     构建 BuildFieldCondition 方法对应的业务数据
    /// </summary>
    /// <param name="parameter">参数信息</param>
    /// <param name="filter">动态筛选条件</param>
    /// <param name="aliases">方法参数 aliases</param>
    /// <returns>BuildFieldCondition 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">筛选字段或操作符缺失时抛出</exception>
    private static Expression BuildFieldCondition(
        ParameterExpression parameter
        , DynamicFilter filter
        , IReadOnlyDictionary<string, string>? aliases
    ) {
        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Operator)) {
            throw new DynamicFilterValidationException("Dynamic filter field and operator are required.");
        }

        var field = filter.Field.Trim();
        if (aliases is not null && aliases.TryGetValue(field, out var mappedField)) {
            field = mappedField;
        }

        var segments = field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0
            ? throw new DynamicFilterValidationException("Dynamic filter field is required.")
            : BuildPathCondition(parameter, segments, 0, filter);
    }

    /// <summary>
    ///     根据操作符构建最终字段条件
    /// </summary>
    /// <param name="member">实体字段表达式</param>
    /// <param name="filter">动态筛选节点</param>
    /// <returns>字段条件表达式</returns>
    /// <exception cref="DynamicFilterValidationException">筛选值或操作符无效时抛出</exception>
    private static Expression BuildMemberCondition(
        MemberExpression member
        , DynamicFilter filter
    ) {
        var operation = filter.Operator!.Trim();
        var operationName = operation.ToUpperInvariant();
        var value = filter.Value ?? default;
        return filter.Value is null && operationName is not ("EQUAL" or "EQUALS" or "EQ" or "NOTEQUAL")
            ? throw new DynamicFilterValidationException("Dynamic filter value is required.")
            : operationName switch
            {
                "CONTAINS" => BuildStringCondition(member, value, nameof(string.Contains), false)
                , "STARTSWITH" => BuildStringCondition(member, value, nameof(string.StartsWith), false)
                , "ENDSWITH" => BuildStringCondition(member, value, nameof(string.EndsWith), false)
                , "NOTCONTAINS" => BuildStringCondition(member, value, nameof(string.Contains), true)
                , "NOTSTARTSWITH" => BuildStringCondition(member, value, nameof(string.StartsWith), true)
                , "NOTENDSWITH" => BuildStringCondition(member, value, nameof(string.EndsWith), true)
                , "EQUAL" or "EQUALS" or "EQ" => Expression.Equal(member, BuildConstant(member.Type, value))
                , "NOTEQUAL" => Expression.NotEqual(member, BuildConstant(member.Type, value))
                , "GREATERTHAN" => BuildComparison(member, value, Expression.GreaterThan)
                , "GREATERTHANOREQUAL" => BuildComparison(member, value, Expression.GreaterThanOrEqual)
                , "LESSTHAN" => BuildComparison(member, value, Expression.LessThan)
                , "LESSTHANOREQUAL" => BuildComparison(member, value, Expression.LessThanOrEqual)
                , "RANGE" => BuildRange(member, value, false)
                , "DATERANGE" => BuildRange(member, value, true)
                , "ANY" => BuildAny(member, value, false)
                , "NOTANY" => BuildAny(member, value, true)
                , "CUSTOM" => throw new DynamicFilterValidationException("Dynamic filter operator Custom is not supported.")
                , _ => throw new DynamicFilterValidationException($"Unsupported dynamic filter operator '{filter.Operator}'.")
            };
    }

    /// <summary>
    ///     沿实体属性路径构建筛选表达式，集合导航属性自动转换为 Any 子查询
    /// </summary>
    /// <param name="current">当前属性路径表达式</param>
    /// <param name="segments">属性路径片段</param>
    /// <param name="index">当前路径片段索引</param>
    /// <param name="filter">动态筛选节点</param>
    /// <returns>字段筛选表达式</returns>
    /// <exception cref="DynamicFilterValidationException">属性路径或集合类型无效时抛出</exception>
    private static Expression BuildPathCondition(
        Expression current
        , IReadOnlyList<string> segments
        , int index
        , DynamicFilter filter
    ) {
        var property = current.Type.GetProperty(segments[index], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null || property.GetIndexParameters().Length != 0) {
            throw new DynamicFilterValidationException($"Dynamic filter field '{string.Join('.', segments)}' is not available.");
        }

        var member = Expression.Property(current, property);
        if (!IsCollection(property.PropertyType)) {
            return index >= segments.Count - 1 ? BuildMemberCondition(member, filter) : BuildPathCondition(member, segments, index + 1, filter);
        }

        if (index == segments.Count - 1) {
            throw new DynamicFilterValidationException($"Dynamic filter field '{string.Join('.', segments)}' is not available.");
        }

        if (index == segments.Count - 2 && string.Equals(segments[index + 1], nameof(ICollection.Count), StringComparison.OrdinalIgnoreCase)) {
            var countProperty = property.PropertyType.GetProperty(nameof(ICollection.Count), BindingFlags.Instance | BindingFlags.Public)
                                ?? throw new DynamicFilterValidationException(
                                    $"Dynamic filter field '{string.Join('.', segments)}' is not available."
                                );
            return BuildMemberCondition(Expression.Property(member, countProperty), filter);
        }

        var elementType = GetCollectionElementType(property.PropertyType);
        var element = Expression.Parameter(elementType, "item");
        var (predicateFilter, negate) = NormalizeCollectionFilter(filter);
        var predicate = BuildPathCondition(element, segments, index + 1, predicateFilter);
        var any = Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [elementType], member, Expression.Lambda(predicate, element));
        return negate ? Expression.Not(any) : any;
    }

    /// <summary>
    ///     构建 BuildRange 方法对应的业务数据
    /// </summary>
    /// <param name="member">成员名称</param>
    /// <param name="value">待处理的值</param>
    /// <param name="dateRange">方法参数 dateRange</param>
    /// <returns>BuildRange 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">范围筛选值数量无效时抛出</exception>
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
        var upperValue = dateRange switch
        {
            true when IsSameDateAtMidnight(values[0], values[1]) => CreateStringElement(
                ParseDate(values[1]).AddDays(1).ToString("O", CultureInfo.InvariantCulture)
            )
            , true => GetDateRangeEnd(values[1])
            , _ => values[1]
        };

        var upper = BuildComparison(member, upperValue, Expression.LessThan);
        return Expression.AndAlso(lower, upper);
    }

    /// <summary>
    ///     构建 BuildStringCondition 方法对应的业务数据
    /// </summary>
    /// <param name="member">成员名称</param>
    /// <param name="value">待处理的值</param>
    /// <param name="method">方法参数 method</param>
    /// <param name="negate">方法参数 negate</param>
    /// <returns>BuildStringCondition 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">字段类型或字符串筛选值无效时抛出</exception>
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

    /// <summary>
    ///     创建 CreateStringElement 方法对应的业务数据
    /// </summary>
    /// <param name="value">待处理的值</param>
    /// <returns>CreateStringElement 方法的执行结果</returns>
    private static JsonElement CreateStringElement(string value) {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    /// <summary>
    ///     获取集合导航属性的元素类型
    /// </summary>
    /// <param name="collectionType">集合属性类型</param>
    /// <returns>集合元素类型</returns>
    /// <exception cref="DynamicFilterValidationException">无法解析集合元素类型时抛出</exception>
    private static Type GetCollectionElementType(Type collectionType) {
        if (collectionType.IsArray) {
            return collectionType.GetElementType()!;
        }

        var enumerableType = collectionType
            .GetInterfaces()
            .Append(collectionType)
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerableType?.GetGenericArguments()[0]
               ?? throw new DynamicFilterValidationException("Dynamic filter collection element type is not available.");
    }

    /// <summary>
    ///     获取 GetDateRangeEnd 方法对应的业务数据
    /// </summary>
    /// <param name="value">待处理的值</param>
    /// <returns>GetDateRangeEnd 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">日期范围结束值缺失或格式无效时抛出</exception>
    private static JsonElement GetDateRangeEnd(JsonElement value) {
        var text = value.GetString() ?? throw new DynamicFilterValidationException("Dynamic filter DateRange end value is required.");
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset)) {
            return CreateStringElement(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
        }

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

    /// <summary>
    ///     判断 IsCollection 方法对应的业务数据
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>IsCollection 方法的执行结果</returns>
    private static bool IsCollection(Type type) {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <summary>
    ///     判断目标类型是否为可转换的基础数字类型
    /// </summary>
    /// <param name="type">待判断的类型</param>
    /// <returns>是数字类型时返回 true，否则返回 false</returns>
    private static bool IsNumericType(Type type) {
        return Type.GetTypeCode(type) is TypeCode.Byte
            or TypeCode.SByte
            or TypeCode.Int16
            or TypeCode.UInt16
            or TypeCode.Int32
            or TypeCode.UInt32
            or TypeCode.Int64
            or TypeCode.UInt64
            or TypeCode.Single
            or TypeCode.Double
            or TypeCode.Decimal;
    }

    /// <summary>
    ///     判断 IsSameDateAtMidnight 方法对应的业务数据
    /// </summary>
    /// <param name="start">方法参数 start</param>
    /// <param name="end">方法参数 end</param>
    /// <returns>IsSameDateAtMidnight 方法的执行结果</returns>
    private static bool IsSameDateAtMidnight(
        JsonElement start
        , JsonElement end
    ) {
        var startDate = ParseDate(start);
        var endDate = ParseDate(end);
        return startDate.Date == endDate.Date && endDate.TimeOfDay == TimeSpan.Zero;
    }

    /// <summary>
    ///     将集合字段的否定操作符转换为对 Any 结果取反，避免多元素集合产生错误语义
    /// </summary>
    /// <param name="filter">动态筛选节点</param>
    /// <returns>集合元素条件以及是否对 Any 结果取反</returns>
    private static (DynamicFilter Filter, bool Negate) NormalizeCollectionFilter(DynamicFilter filter) {
        var operation = filter.Operator?.Trim().ToUpperInvariant();
        var positiveOperation = operation switch
        {
            "NOTCONTAINS" => "Contains"
            , "NOTSTARTSWITH" => "StartsWith"
            , "NOTENDSWITH" => "EndsWith"
            , "NOTEQUAL" => "Equal"
            , "NOTANY" => "Any"
            , _ => null
        };
        return positiveOperation is null
            ? (filter, false)
            : (new DynamicFilter { Field = filter.Field, Operator = positiveOperation, Value = filter.Value }, true);
    }

    /// <summary>
    ///     解析 ParseDate 方法对应的业务数据
    /// </summary>
    /// <param name="value">待处理的值</param>
    /// <returns>ParseDate 方法的执行结果</returns>
    /// <exception cref="DynamicFilterValidationException">日期值缺失或格式无效时抛出</exception>
    private static DateTimeOffset ParseDate(JsonElement value) {
        var text = value.GetString() ?? throw new DynamicFilterValidationException("Dynamic filter DateRange date is required.");
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : throw new DynamicFilterValidationException("Dynamic filter DateRange date format is invalid.");
    }

    /// <summary>
    ///     将枚举名称或数字文本转换为枚举值
    /// </summary>
    /// <param name="targetType">目标枚举类型</param>
    /// <param name="value">枚举名称或数字文本</param>
    /// <returns>转换后的枚举值</returns>
    private static object ParseEnumValue(
        Type targetType
        , string value
    ) {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            ? Enum.ToObject(targetType, numeric)
            : Enum.Parse(targetType, value, true);
    }

    /// <summary>
    ///     将 JSON 筛选值转换为实体字段类型
    /// </summary>
    /// <param name="value">JSON 筛选值</param>
    /// <param name="targetType">实体字段类型</param>
    /// <returns>转换后的字段值</returns>
    /// <exception cref="DynamicFilterValidationException">筛选值无法转换为实体字段类型时抛出</exception>
    private static object? ReadValue(
        JsonElement value
        , Type targetType
    ) {
        try {
            return targetType switch
            {
                _ when targetType == typeof(string) => value.GetString() ?? string.Empty
                , _ when targetType == typeof(DateTimeOffset) => DateTimeOffset.Parse(
                    value.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind
                )
                , _ when targetType == typeof(DateTime) => DateTimeOffset.Parse(
                        value.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind
                    )
                    .UtcDateTime
                , _ when targetType.IsEnum && value.ValueKind == JsonValueKind.String => ParseEnumValue(targetType, value.GetString() ?? string.Empty)
                , _ when IsNumericType(targetType) && value.ValueKind == JsonValueKind.String => Convert.ChangeType(
                    value.GetString() ?? string.Empty, targetType, CultureInfo.InvariantCulture
                )
                , _ when targetType == typeof(bool) && value.ValueKind == JsonValueKind.String => bool.Parse(value.GetString() ?? string.Empty)
                , _ => JsonSerializer.Deserialize(value.GetRawText(), targetType, JsonParsing.Options)
            };
        }
        catch (Exception exception) when (exception is JsonException
                                              or FormatException
                                              or OverflowException
                                              or ArgumentException
                                              or InvalidCastException) {
            throw new DynamicFilterValidationException($"Dynamic filter value cannot be converted to {targetType.Name}.");
        }
    }

    /// <summary>
    ///     读取 ReadValues 方法对应的业务数据
    /// </summary>
    /// <param name="value">待处理的值</param>
    /// <returns>ReadValues 方法的执行结果</returns>
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

    /// <summary>
    ///     解包 Unwrap 方法对应的业务数据
    /// </summary>
    /// <param name="filter">动态筛选条件</param>
    /// <returns>Unwrap 方法的执行结果</returns>
    private static DynamicFilter Unwrap(DynamicFilter filter) {
        var current = filter;
        while (current.NestedDynamicFilter is not null) {
            current = current.NestedDynamicFilter;
        }

        return current;
    }
}