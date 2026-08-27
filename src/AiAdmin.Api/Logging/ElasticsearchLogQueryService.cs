using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     从 Elasticsearch 查询系统日志
/// </summary>
/// <param name="httpClient">HTTP 客户端</param>
/// <param name="options">Elasticsearch 配置</param>
public sealed class ElasticsearchLogQueryService(HttpClient httpClient, IOptions<ElasticsearchLogOptions> options)
{
    private const int _MAX_RESULT_WINDOW = 10000;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     分页查询系统日志
    /// </summary>
    /// <param name="request">日志查询请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>日志分页结果</returns>
    /// <exception cref="HttpRequestException">Elasticsearch 请求失败</exception>
    /// <exception cref="InvalidOperationException">Elasticsearch 返回的日志文档格式无效</exception>
    public async Task<(IReadOnlyList<SystemLogItem> Records, int Total)> SearchAsync(
        SystemLogQueryRequest request
        , CancellationToken cancellationToken
    ) {
        if (!options.Value.Enabled) {
            return ([], 0);
        }

        var current = request.Current;
        var size = request.Size;

        // Art 筛选器会将分页字段一并包装进 dynamicFilter，ES 查询前拆出分页参数。
        var logFilter = SplitPaginationFilters(request.DynamicFilter, ref current, ref size);
        var must = new List<object>();
        var dynamicQuery = BuildDynamicQuery(logFilter);
        if (dynamicQuery is not null) {
            must.Add(dynamicQuery);
        }

        var from = (long)(Math.Max(current, 1) - 1) * Math.Max(size, 1);
        var isDeepPage = from >= _MAX_RESULT_WINDOW;
        var resultSize = isDeepPage ? 0 : Math.Min(Math.Max(size, 1), _MAX_RESULT_WINDOW - (int)from);
        object query = must.Count == 0 ? new { match_all = new { } } : new { @bool = new { must } };
        var payload = JsonSerializer.Serialize(
            new
            {
                from = isDeepPage ? 0 : (int)from
                , size = resultSize
                , track_total_hits = true
                , query
                , sort = new[]
                {
                    new Dictionary<string, object>
                    {
                        [ResolveSortField(request.SortField)] = new
                        {
                            order = string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc"
                        }
                    }
                }
            }
        );
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.Uri.TrimEnd('/')}/{options.Value.Index}/_search");
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        AddAuthentication(httpRequest);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Elasticsearch query failed with {(int)response.StatusCode}: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var totalElement = root.GetProperty("hits").GetProperty("total");
        var total = totalElement.ValueKind == JsonValueKind.Object ? totalElement.GetProperty("value").GetInt32() : totalElement.GetInt32();
        var records = root
            .GetProperty("hits")
            .GetProperty("hits")
            .EnumerateArray()
            .Select(hit => hit.GetProperty("_source"))
            .Select(source => JsonSerializer.Deserialize<SystemLogItem>(source.GetRawText(), _jsonOptions)
                              ?? throw new InvalidOperationException("Invalid Elasticsearch log document")
            )
            .ToList();

        return (records, total);
    }

    private static object? BuildDynamicQuery(DynamicFilter? filter) {
        if (filter is null) {
            return null;
        }

        if (filter.Filters.Count > 0) {
            var children = filter.Filters.Select(BuildDynamicQuery).Where(x => x is not null).ToArray();
            var logic = filter.Logic.Equals("Or", StringComparison.OrdinalIgnoreCase) ? "should" : "must";

            return children.Length == 0 ? null : new Dictionary<string, object> { ["bool"] = new Dictionary<string, object> { [logic] = children } };
        }

        var field = ResolveSearchField(filter.Field);
        if (field is null || string.IsNullOrWhiteSpace(filter.Operator) || filter.Value is null) {
            return null;
        }

        var value = filter.Value.Value;
        if ((filter.Operator.Equals("DateRange", StringComparison.OrdinalIgnoreCase)
             || (filter.Operator.Equals("Any", StringComparison.OrdinalIgnoreCase) && field == "timestamp"))
            && value.ValueKind == JsonValueKind.Array) {
            // 兼容前端日期范围在部分查询路径中多包一层数组的请求格式
            var rangeValue = value.GetArrayLength() == 1 && value[0].ValueKind == JsonValueKind.Array ? value[0] : value;
            if (rangeValue.GetArrayLength() < 2) {
                return null;
            }

            var rangeValues = rangeValue.EnumerateArray().Take(2).ToArray();
            return new
            {
                range = new Dictionary<string, object>
                {
                    [field] = new { gte = GetScalarValue(rangeValues[0]), lt = GetScalarValue(rangeValues[1]) }
                }
            };
        }

        var text = value.ToString();
        if (value.ValueKind == JsonValueKind.String) {
            text = value.GetString() ?? string.Empty;
        }

        var normalizedOperator = filter.Operator.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalizedOperator switch
        {
            "ANY" when value.ValueKind == JsonValueKind.Array => new
            {
                terms = new Dictionary<string, object> { [field] = value.EnumerateArray().Select(GetScalarValue).ToArray() }
            }
            , "EQUAL" or "EQUALS" => new { match = new Dictionary<string, object> { [field] = text } }
            , "NOTEQUAL" or "NOTEQUALS" => new
            {
                @bool = new { must_not = new[] { new { match = new Dictionary<string, object> { [field] = text } } } }
            }
            , "CONTAINS" when field == "level" => new { match = new Dictionary<string, object> { [field] = text } }
            , "CONTAINS" => new { wildcard = new Dictionary<string, object> { [field] = $"*{EscapeWildcard(text)}*" } }
            , "STARTSWITH" => new { wildcard = new Dictionary<string, object> { [field] = $"{EscapeWildcard(text)}*" } }
            , "ENDSWITH" => new { wildcard = new Dictionary<string, object> { [field] = $"*{EscapeWildcard(text)}" } }
            , "GREATERTHAN" => new { range = new Dictionary<string, object> { [field] = new { gt = GetScalarValue(value) } } }
            , "GREATERTHANOREQUAL" => new { range = new Dictionary<string, object> { [field] = new { gte = GetScalarValue(value) } } }
            , "LESSTHAN" => new { range = new Dictionary<string, object> { [field] = new { lt = GetScalarValue(value) } } }
            , "LESSTHANOREQUAL" => new { range = new Dictionary<string, object> { [field] = new { lte = GetScalarValue(value) } } }
            , _ => new { match = new Dictionary<string, object> { [field] = text } }
        };
    }

    private static string EscapeWildcard(string value) {
        return value.Replace("*", "\\*", StringComparison.Ordinal).Replace("?", "\\?", StringComparison.Ordinal);
    }

    private static object GetScalarValue(JsonElement value) {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value;
    }

    /// <summary>
    ///     判断字段是否为分页参数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <returns>是否为分页参数</returns>
    private static bool IsPaginationField(string? field) {
        return field?.Equals("current", StringComparison.OrdinalIgnoreCase) == true
               || field?.Equals("size", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? ResolveSearchField(string? searchField) {
        return searchField?.ToLowerInvariant() switch
        {
            "category" => "category"
            , "clientip" => "clientIp"
            , "elapsedmilliseconds" => "elapsedMilliseconds"
            , "eventid" => "eventId"
            , "eventname" => "eventName"
            , "exception" => "exception"
            , "level" => "level"
            , "logtype" => "logType"
            , "message" => "message"
            , "requestbody" => "requestBody"
            , "requestcontenttype" => "requestContentType"
            , "requestheaders" => "requestHeaders"
            , "requestmethod" => "requestMethod"
            , "requestrelativeurl" => "requestRelativeUrl"
            , "requesturl" => "requestUrl"
            , "responsebody" => "responseBody"
            , "responsecontenttype" => "responseContentType"
            , "responseheaders" => "responseHeaders"
            , "serverip" => "serverIp"
            , "source" => "source"
            , "sql" => "sql"
            , "statuscode" => "statusCode"
            , "threadid" => "threadId"
            , "timestamp" => "timestamp"
            , "traceid" => "traceId"
            , "useragent" => "userAgent"
            , "username" => "userName"
            , _ => null
        };
    }

    private static string ResolveSortField(string? sortField) {
        return sortField?.ToLowerInvariant() switch
        {
            "category" => "category"
            , "clientip" => "clientIp"
            , "elapsedmilliseconds" => "elapsedMilliseconds"
            , "eventid" => "eventId"
            , "eventname" => "eventName"
            , "level" => "level"
            , "logtype" => "logType"
            , "source" => "source"
            , "statuscode" => "statusCode"
            , "threadid" => "threadId"
            , "username" => "userName"
            , _ => "timestamp"
        };
    }

    /// <summary>
    ///     处理动态筛选树节点并提取其中的分页条件
    /// </summary>
    /// <param name="filter">筛选节点</param>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <returns>清理后的筛选节点</returns>
    private static DynamicFilter? SplitPaginationFilterNode(
        DynamicFilter filter
        , ref int current
        , ref int size
    ) {
        if (TryReadPagination(filter, ref current, ref size)) {
            return null;
        }

        if (filter.Filters.Count > 0 || filter.NestedDynamicFilter is not null) {
            return SplitPaginationFilters(filter, ref current, ref size);
        }

        return filter;
    }

    /// <summary>
    ///     从动态筛选树中拆出分页条件
    /// </summary>
    /// <param name="filter">原始动态筛选条件</param>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <returns>仅包含 Elasticsearch 日志字段的筛选条件</returns>
    private static DynamicFilter? SplitPaginationFilters(
        DynamicFilter? filter
        , ref int current
        , ref int size
    ) {
        if (filter is null) {
            return null;
        }

        if (filter.NestedDynamicFilter is not null) {
            return SplitPaginationFilters(filter.NestedDynamicFilter, ref current, ref size);
        }

        if (filter.Filters.Count == 0) {
            return TryReadPagination(filter, ref current, ref size) ? null : filter;
        }

        var filters = new List<DynamicFilter>();
        foreach (var child in filter.Filters) {
            var cleaned = SplitPaginationFilterNode(child, ref current, ref size);
            if (cleaned is not null) {
                filters.Add(cleaned);
            }
        }

        return filters.Count switch
        {
            0 => null
            , 1 => filters[0]
            , _ => new DynamicFilter { Logic = filter.Logic, Filters = filters }
        };
    }

    /// <summary>
    ///     将 JSON 值转换为整数
    /// </summary>
    /// <param name="value">JSON 值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    private static bool TryGetInt(
        JsonElement value
        , out int result
    ) {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result)) {
            return true;
        }

        if (value.ValueKind == JsonValueKind.String) {
            return int.TryParse(value.GetString(), out result);
        }

        result = 0;
        return false;
    }

    /// <summary>
    ///     尝试读取当前节点中的分页条件
    /// </summary>
    /// <param name="filter">筛选节点</param>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <returns>节点是否为有效分页条件</returns>
    private static bool TryReadPagination(
        DynamicFilter filter
        , ref int current
        , ref int size
    ) {
        if (!IsPaginationField(filter.Field) || filter.Value is not { } value || !TryGetInt(value, out var parsed)) {
            return false;
        }

        if (filter.Field!.Equals("current", StringComparison.OrdinalIgnoreCase)) {
            current = parsed;
        }
        else {
            size = parsed;
        }

        return true;
    }

    private void AddAuthentication(HttpRequestMessage request) {
        if (string.IsNullOrWhiteSpace(options.Value.Username)) {
            return;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.Username}:{options.Value.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}