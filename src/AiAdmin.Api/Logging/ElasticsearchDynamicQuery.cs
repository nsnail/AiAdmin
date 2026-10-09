using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Logging;

/// <summary>
///     将公共动态筛选协议转换为 Elasticsearch 查询并保持字段类型与条件分组
/// </summary>
public static class ElasticsearchDynamicQuery
{
    private const int _MAX_RESULT_WINDOW = 10000;

    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["keyword"] = nameof(SystemLogItem.Message), ["CreatedAt"] = nameof(SystemLogItem.Timestamp)
    };

    /// <summary>
    ///     校验查询并构建 Elasticsearch 请求正文
    /// </summary>
    /// <param name="request">公共分页筛选与排序请求</param>
    /// <returns>Elasticsearch 搜索请求 JSON</returns>
    /// <exception cref="DynamicFilterValidationException">查询参数无效</exception>
    public static string Build(DynamicQueryRequest request) {
        if (request.Current < 1 || request.Size is < 1 or > 100) {
            throw new DynamicFilterValidationException("Current must be positive and size must be between 1 and 100.");
        }

        // 复用实体查询校验，避免 Elasticsearch 静默忽略非法字段、操作符或值类型
        _ = Array
            .Empty<SystemLogItem>()
            .AsQueryable()
            .ApplyDynamicFilter(request.DynamicFilter, _aliases)
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(SystemLogItem.Timestamp), true, _aliases);
        var runtimeFields = new Dictionary<string, object>();
        var query = BuildNode(request.DynamicFilter, runtimeFields) ?? new { match_all = new { } };
        var sortProperty = ResolveProperty(string.IsNullOrWhiteSpace(request.SortField) ? nameof(SystemLogItem.Timestamp) : request.SortField);
        var sortField = ResolveField(sortProperty, runtimeFields);
        var descending = string.IsNullOrWhiteSpace(request.SortField) || string.Equals(request.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var sortType = sortProperty.PropertyType switch
        {
            var type when type == typeof(string) => "keyword"
            , var type when type == typeof(DateTimeOffset) => "date"
            , _ => "long"
        };
        var sort = new List<object>
        {
            new Dictionary<string, object>
            {
                [sortField] = new { order = descending ? "desc" : "asc", missing = "_last", unmapped_type = sortType }
            }
        };
        if (sortProperty.Name != nameof(SystemLogItem.Timestamp)) {
            sort.Add(new { timestamp = new { order = "desc", unmapped_type = "date" } });
        }

        // 相同业务排序值使用索引文档顺序兜底，避免仅依赖相关性评分
        sort.Add(new { _doc = new { order = "asc" } });
        var from = (long)(request.Current - 1) * request.Size;
        return JsonSerializer.Serialize(
            new
            {
                from = from >= _MAX_RESULT_WINDOW ? 0 : from
                , size = from >= _MAX_RESULT_WINDOW ? 0 : Math.Min(request.Size, _MAX_RESULT_WINDOW - (int)from)
                , track_total_hits = true
                , runtime_mappings = runtimeFields
                , query
                , sort
            }
        );
    }

    /// <summary>
    ///     转换字段操作符，范围统一使用左闭右开语义
    /// </summary>
    /// <param name="property">日志字段属性</param>
    /// <param name="field">Elasticsearch 字段名</param>
    /// <param name="operation">动态查询操作符</param>
    /// <param name="value">筛选 JSON 值</param>
    /// <returns>字段查询对象</returns>
    /// <exception cref="DynamicFilterValidationException">操作符不受支持</exception>
    private static object BuildField(
        PropertyInfo property
        , string field
        , string operation
        , JsonElement value
    ) {
        var op = operation.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return op switch
        {
            "ANY" or "NOTANY" => BuildSet(property, field, op, value)
            , "RANGE" or "DATERANGE" => BuildRange(property, field, op, value)
            , _ => BuildScalar(field, op, ConvertValue(property, value), operation)
        };
    }

    /// <summary>
    ///     递归转换节点并保留同一节点上的字段条件与子条件
    /// </summary>
    /// <param name="filter">动态筛选节点</param>
    /// <param name="runtimeFields">按需添加的原始字符串字段</param>
    /// <returns>查询对象，空条件返回 null</returns>
    private static object? BuildNode(
        DynamicFilter? filter
        , Dictionary<string, object> runtimeFields
    ) {
        if (filter is null) {
            return null;
        }

        if (filter.NestedDynamicFilter is not null) {
            return BuildNode(filter.NestedDynamicFilter, runtimeFields);
        }

        var children = new List<object>();
        if (!string.IsNullOrWhiteSpace(filter.Field) || !string.IsNullOrWhiteSpace(filter.Operator)) {
            var property = ResolveProperty(filter.Field!);
            children.Add(BuildField(property, ResolveField(property, runtimeFields), filter.Operator!, filter.Value ?? default));
        }

        children.AddRange(filter.Filters.Select(child => BuildNode(child, runtimeFields)).OfType<object>());
        return children.Count == 0
            ? null
            : (object)(filter.Logic.Equals("Or", StringComparison.OrdinalIgnoreCase)
                ? new { @bool = (object)new { should = children, minimum_should_match = 1 } }
                : new { @bool = (object)new { filter = children } });
    }

    /// <summary>
    ///     构建左闭右开范围并处理日期结束边界
    /// </summary>
    /// <param name="property">日志字段属性</param>
    /// <param name="field">Elasticsearch 字段名</param>
    /// <param name="op">标准化范围操作符</param>
    /// <param name="value">范围 JSON 值</param>
    /// <returns>范围查询对象</returns>
    private static object BuildRange(
        PropertyInfo property
        , string field
        , string op
        , JsonElement value
    ) {
        var values = ReadValues(value);
        var lower = ConvertValue(property, values[0]);
        var upper = ConvertValue(property, values[1]);
        if (op != "DATERANGE" || upper is not DateTimeOffset end) {
            return new { range = new Dictionary<string, object> { [field] = new { gte = lower, lt = upper } } };
        }

        var endText = values[1].GetString() ?? string.Empty;
        if (endText.Length == 10 || (lower is DateTimeOffset start && start == end && end.TimeOfDay == TimeSpan.Zero)) {
            upper = end.AddDays(1);
        }

        return new { range = new Dictionary<string, object> { [field] = new { gte = lower, lt = upper } } };
    }

    /// <summary>
    ///     按标量操作符构建精确匹配、比较或字符串查询
    /// </summary>
    /// <param name="field">Elasticsearch 字段名</param>
    /// <param name="op">标准化操作符</param>
    /// <param name="scalar">已转换的字段值</param>
    /// <param name="operation">原始操作符，用于错误提示</param>
    /// <returns>标量查询对象</returns>
    /// <exception cref="DynamicFilterValidationException">操作符不受支持</exception>
    private static object BuildScalar(
        string field
        , string op
        , object? scalar
        , string operation
    ) {
        switch (op) {
            case "EQUAL" or "EQUALS" or "EQ":
                return Equal(field, scalar);
            case "NOTEQUAL":
                return Negate(Equal(field, scalar));
            case "GREATERTHAN" or "GREATERTHANOREQUAL" or "LESSTHAN" or "LESSTHANOREQUAL":
                var comparison = op switch
                {
                    "GREATERTHAN" => "gt"
                    , "GREATERTHANOREQUAL" => "gte"
                    , "LESSTHAN" => "lt"
                    , _ => "lte"
                };
                return new { range = new Dictionary<string, object> { [field] = new Dictionary<string, object?> { [comparison] = scalar } } };

            case "CONTAINS" or "STARTSWITH" or "ENDSWITH" or "NOTCONTAINS" or "NOTSTARTSWITH" or "NOTENDSWITH":
                return BuildWildcard(field, op, scalar);

            default:
                throw new DynamicFilterValidationException($"Dynamic filter operator '{operation}' is not supported.");
        }
    }

    /// <summary>
    ///     构建集合匹配并保留集合中的空值语义
    /// </summary>
    /// <param name="property">日志字段属性</param>
    /// <param name="field">Elasticsearch 字段名</param>
    /// <param name="op">标准化集合操作符</param>
    /// <param name="value">集合 JSON 值</param>
    /// <returns>集合查询对象</returns>
    private static object BuildSet(
        PropertyInfo property
        , string field
        , string op
        , JsonElement value
    ) {
        var values = ReadValues(value).Select(item => ConvertValue(property, item)).ToArray();
        var terms = values.Where(item => item is not null).ToArray();
        object query = new { terms = new Dictionary<string, object> { [field] = terms } };
        if (values.Any(item => item is null)) {
            query = new { @bool = new { should = new[] { query, Equal(field, null) }, minimum_should_match = 1 } };
        }

        return op == "NOTANY" ? Negate(query) : query;
    }

    /// <summary>
    ///     构建字符串通配符查询，否定匹配排除空字段
    /// </summary>
    /// <param name="field">Elasticsearch 字段名</param>
    /// <param name="op">标准化字符串操作符</param>
    /// <param name="scalar">已转换的字符串值</param>
    /// <returns>字符串查询对象</returns>
    private static object BuildWildcard(
        string field
        , string op
        , object? scalar
    ) {
        var text = EscapeWildcard((string?)scalar ?? string.Empty);
        var positive = op.StartsWith("NOT", StringComparison.Ordinal) ? op[3..] : op;
        var pattern = positive switch
        {
            "STARTSWITH" => $"{text}*"
            , "ENDSWITH" => $"*{text}"
            , _ => $"*{text}*"
        };
        object query = new { wildcard = new Dictionary<string, object> { [field] = pattern } };

        // 与公共字符串查询一致，否定包含不匹配 null 字段
        return op.StartsWith("NOT", StringComparison.Ordinal)
            ? new { @bool = new { filter = new[] { new { exists = new { field } } }, must_not = new[] { query } } }
            : query;
    }

    /// <summary>
    ///     将已校验的 JSON 标量转换为模型字段类型
    /// </summary>
    /// <param name="property">目标属性</param>
    /// <param name="value">JSON 标量</param>
    /// <returns>保留数值、日期和空值类型的查询值</returns>
    private static object? ConvertValue(
        PropertyInfo property
        , JsonElement value
    ) {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return type == typeof(string)
            ? value.GetString()
            : type == typeof(DateTimeOffset)
                ? DateTimeOffset.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : value.ValueKind == JsonValueKind.String
                    ? Convert.ChangeType(value.GetString(), type, CultureInfo.InvariantCulture)
                    : JsonSerializer.Deserialize(value.GetRawText(), type, JsonParsing.Options);
    }

    /// <summary>
    ///     构建精确匹配，空值使用字段不存在语义
    /// </summary>
    /// <param name="field">查询字段</param>
    /// <param name="value">已转换的字段值</param>
    /// <returns>精确匹配查询</returns>
    private static object Equal(
        string field
        , object? value
    ) {
        return value is null ? Negate(new { exists = new { field } }) : new { term = new Dictionary<string, object> { [field] = value } };
    }

    /// <summary>
    ///     转义用户文本中的通配符与转义字符
    /// </summary>
    /// <param name="value">用户筛选文字</param>
    /// <returns>按字面值匹配的文本</returns>
    private static string EscapeWildcard(string value) {
        return value
            .Replace("\\", @"\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("?", "\\?", StringComparison.Ordinal);
    }

    /// <summary>
    ///     对查询取反
    /// </summary>
    /// <param name="query">原查询</param>
    /// <returns>否定查询</returns>
    private static object Negate(object query) {
        return new { @bool = new { must_not = new[] { query } } };
    }

    /// <summary>
    ///     读取集合或范围值，与公共动态查询的逗号分隔格式保持一致
    /// </summary>
    /// <param name="value">集合或范围 JSON</param>
    /// <returns>标量值列表</returns>
    private static JsonElement[] ReadValues(JsonElement value) {
        return value.ValueKind switch
        {
            JsonValueKind.Array => [.. value.EnumerateArray()]
            , JsonValueKind.String =>
            [
                .. (value.GetString() ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => JsonSerializer.SerializeToElement(item))
            ]
            , _ => [value]
        };
    }

    /// <summary>
    ///     按需从原始文档暴露字符串，避免分词或 keyword 长度上限改变筛选语义
    /// </summary>
    /// <param name="property">日志属性</param>
    /// <param name="runtimeFields">本次请求使用的运行时字段</param>
    /// <returns>Elasticsearch 查询字段</returns>
    private static string ResolveField(
        PropertyInfo property
        , Dictionary<string, object> runtimeFields
    ) {
        var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        if (property.PropertyType != typeof(string)) {
            return name;
        }

        var runtimeName = $"filter_{name}";
        runtimeFields[runtimeName] = new
        {
            type = "keyword"
            , script = new
            {
                source = "def value = params._source[params.field]; if (value != null) { emit(value.toString()); }"
                , @params = new { field = name }
            }
        };
        return runtimeName;
    }

    /// <summary>
    ///     解析字段别名与大小写并拒绝未知字段
    /// </summary>
    /// <param name="field">客户端字段名</param>
    /// <returns>日志模型属性</returns>
    /// <exception cref="DynamicFilterValidationException">字段不存在</exception>
    private static PropertyInfo ResolveProperty(string field) {
        var name = field.Trim();
        if (_aliases.TryGetValue(name, out var alias)) {
            name = alias;
        }

        return typeof(SystemLogItem).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
               ?? throw new DynamicFilterValidationException($"Dynamic query field '{field}' is not available.");
    }
}