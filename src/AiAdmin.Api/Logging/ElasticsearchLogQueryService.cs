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
    private const int MaxResultWindow = 10000;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // 通用关键词只查询文本字段，避免 Elasticsearch 将文字转换为数值或日期
    private static readonly string[] _keywordFields = [
        "category", "clientIp", "eventName", "exception", "level", "logType", "message", "requestBody"
        , "requestContentType", "requestHeaders", "requestId", "requestMethod", "requestRelativeUrl", "requestUrl", "responseBody"
        , "responseContentType", "responseHeaders", "serverIp", "source", "sql", "userAgent", "userName"
    ];

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

        var must = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.Level)) {
            must.Add(new { match = new { level = request.Level.Trim() } });
        }

        if (!string.IsNullOrWhiteSpace(request.LogType)) {
            must.Add(new { match = new { logType = request.LogType.Trim() } });
        }

        if (!string.IsNullOrWhiteSpace(request.Category)) {
            must.Add(new { wildcard = new { category = $"*{EscapeWildcard(request.Category.Trim())}*" } });
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword)) {
            var keyword = request.Keyword.Trim();
            var searchField = ResolveSearchField(request.SearchField);
            must.Add(searchField is null
                ? new { multi_match = new { query = keyword, fields = _keywordFields, lenient = true } }
                : new { match = new Dictionary<string, string> { [searchField] = keyword } });
        }

        var dynamicQuery = BuildDynamicQuery(request.DynamicFilter);
        if (dynamicQuery is not null) {
            must.Add(dynamicQuery);
        }

        var from = (long)(Math.Max(request.Current, 1) - 1) * Math.Max(request.Size, 1);
        var isDeepPage = from >= MaxResultWindow;
        var size = isDeepPage ? 0 : Math.Min(Math.Max(request.Size, 1), MaxResultWindow - (int)from);
        object query = must.Count == 0 ? new { match_all = new { } } : new { @bool = new { must } };
        var payload = JsonSerializer.Serialize(
            new
            {
                from = isDeepPage ? 0 : (int)from
                , size
                , track_total_hits = true
                , query
                , sort = new[] { new Dictionary<string, object> {
                    [ResolveSortField(request.SortField)] = new { order = string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc" }
                } }
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

    private static string EscapeWildcard(string value) {
        return value.Replace("*", "\\*", StringComparison.Ordinal).Replace("?", "\\?", StringComparison.Ordinal);
    }

    private static string ResolveSortField(string? sortField) {
        return sortField?.ToLowerInvariant() switch {
            "category" => "category",
            "clientip" => "clientIp",
            "elapsedmilliseconds" => "elapsedMilliseconds",
            "eventid" => "eventId",
            "eventname" => "eventName",
            "level" => "level",
            "logtype" => "logType",
            "source" => "source",
            "statuscode" => "statusCode",
            "threadid" => "threadId",
            "username" => "userName",
            _ => "timestamp"
        };
    }

    private static string? ResolveSearchField(string? searchField) {
        return searchField?.ToLowerInvariant() switch {
            "category" => "category",
            "clientip" => "clientIp",
            "elapsedmilliseconds" => "elapsedMilliseconds",
            "eventid" => "eventId",
            "eventname" => "eventName",
            "exception" => "exception",
            "level" => "level",
            "logtype" => "logType",
            "message" => "message",
            "requestbody" => "requestBody",
            "requestcontenttype" => "requestContentType",
            "requestheaders" => "requestHeaders",
            "requestid" => "requestId",
            "requestmethod" => "requestMethod",
            "requestrelativeurl" => "requestRelativeUrl",
            "requesturl" => "requestUrl",
            "responsebody" => "responseBody",
            "responsecontenttype" => "responseContentType",
            "responseheaders" => "responseHeaders",
            "serverip" => "serverIp",
            "source" => "source",
            "sql" => "sql",
            "statuscode" => "statusCode",
            "threadid" => "threadId",
            "timestamp" => "timestamp",
            "useragent" => "userAgent",
            "username" => "userName",
            _ => null
        };
    }

    private static object? BuildDynamicQuery(DynamicFilter? filter) {
        if (filter is null) {
            return null;
        }

        if (filter.Filters.Count > 0) {
            var children = filter.Filters.Select(BuildDynamicQuery).Where(x => x is not null).ToArray();
            var logic = filter.Logic.Equals("Or", StringComparison.OrdinalIgnoreCase) ? "should" : "must";

            return children.Length == 0 ? null : new Dictionary<string, object> {
                ["bool"] = new Dictionary<string, object> {
                    [logic] = children
                }
            };
        }

        var field = ResolveSearchField(filter.Field);
        if (field is null || string.IsNullOrWhiteSpace(filter.Operator) || filter.Value is null) {
            return null;
        }

        var value = filter.Value.Value;
        if (filter.Operator.Equals("DateRange", StringComparison.OrdinalIgnoreCase)
            && value.ValueKind == JsonValueKind.Array
            && value.GetArrayLength() >= 2) {
            var rangeValues = value.EnumerateArray().Take(2).ToArray();
            return new { range = new Dictionary<string, object> {
                [field] = new { gte = GetScalarValue(rangeValues[0]), lt = GetScalarValue(rangeValues[1]) }
            } };
        }

        var text = value.ToString();
        if (value.ValueKind == JsonValueKind.String) {
            text = value.GetString() ?? string.Empty;
        }

        return filter.Operator.ToUpperInvariant() switch {
            "EQUAL" or "EQUALS" => new { match = new Dictionary<string, object> { [field] = text } },
            "NOT_EQUAL" or "NOT_EQUALS" => new { @bool = new { must_not = new[] { new { match = new Dictionary<string, object> { [field] = text } } } } },
            "CONTAINS" => new { wildcard = new Dictionary<string, object> { [field] = $"*{EscapeWildcard(text)}*" } },
            "STARTS_WITH" => new { wildcard = new Dictionary<string, object> { [field] = $"{EscapeWildcard(text)}*" } },
            "ENDS_WITH" => new { wildcard = new Dictionary<string, object> { [field] = $"*{EscapeWildcard(text)}" } },
            "GREATER_THAN" => new { range = new Dictionary<string, object> { [field] = new { gt = GetScalarValue(value) } } },
            "GREATER_THAN_OR_EQUAL" => new { range = new Dictionary<string, object> { [field] = new { gte = GetScalarValue(value) } } },
            "LESS_THAN" => new { range = new Dictionary<string, object> { [field] = new { lt = GetScalarValue(value) } } },
            "LESS_THAN_OR_EQUAL" => new { range = new Dictionary<string, object> { [field] = new { lte = GetScalarValue(value) } } },
            _ => new { match = new Dictionary<string, object> { [field] = text } }
        };
    }

    private static object GetScalarValue(JsonElement value) {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value;
    }

    private void AddAuthentication(HttpRequestMessage request) {
        if (string.IsNullOrWhiteSpace(options.Value.Username)) {
            return;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.Username}:{options.Value.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}