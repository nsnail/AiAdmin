using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     从 Elasticsearch 查询系统日志
/// </summary>
/// <param name="httpClient">HTTP 客户端</param>
/// <param name="options">Elasticsearch 配置</param>
public sealed class ElasticsearchLogQueryService(HttpClient httpClient, IOptions<ElasticsearchLogOptions> options)
{
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

        var must = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.Level)) {
            must.Add(new { term = new { level = request.Level.Trim() } });
        }

        if (!string.IsNullOrWhiteSpace(request.Category)) {
            must.Add(new { wildcard = new { category = $"*{EscapeWildcard(request.Category.Trim())}*" } });
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword)) {
            must.Add(new { match = new { message = request.Keyword.Trim() } });
        }

        object query = must.Count == 0 ? new { match_all = new { } } : new { @bool = new { must } };
        var payload = JsonSerializer.Serialize(
            new
            {
                from = (request.Current - 1) * request.Size
                , size = request.Size
                , track_total_hits = true
                , query
                , sort = new[] { new { timestamp = new { order = "desc" } } }
            }
        );
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.Uri.TrimEnd('/')}/{options.Value.Index}/_search");
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        AddAuthentication(httpRequest);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
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

    private void AddAuthentication(HttpRequestMessage request) {
        if (string.IsNullOrWhiteSpace(options.Value.Username)) {
            return;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.Username}:{options.Value.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}