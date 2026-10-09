using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.Extensions.Options;

namespace AiAdmin.Api.Logging;

/// <summary>
///     通过统一外部请求服务查询 Elasticsearch 系统日志
/// </summary>
/// <param name="httpRequests">记录外部请求与响应的服务</param>
/// <param name="options">Elasticsearch 配置</param>
public sealed class ElasticsearchLogQueryService(ExternalHttpRequestService httpRequests, IOptions<ElasticsearchLogOptions> options)
{
    /// <summary>
    ///     按公共动态查询协议跨每日索引分页读取日志，无索引时返回空结果
    /// </summary>
    /// <param name="request">日志查询请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>日志记录和匹配总数</returns>
    /// <exception cref="HttpRequestException">Elasticsearch 查询失败</exception>
    /// <exception cref="InvalidOperationException">日志文档格式无效</exception>
    public async Task<(IReadOnlyList<SystemLogItem> Records, int Total)> SearchAsync(
        DynamicQueryRequest request
        , CancellationToken cancellationToken
    ) {
        var payload = ElasticsearchDynamicQuery.Build(request);
        if (!options.Value.Enabled) {
            return ([], 0);
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{options.Value.Uri.TrimEnd('/')}/{options.Value.Index}-*/_search?allow_no_indices=true"
        );
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(options.Value.Username)) {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.Username}:{options.Value.Password}"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await httpRequests.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is < 200 or >= 300) {
            throw new HttpRequestException($"Elasticsearch query failed with {response.StatusCode}: {response.ResponseBody}");
        }

        using var document = JsonDocument.Parse(response.ResponseBody);
        var hits = document.RootElement.GetPropertyIgnoreCase("hits");
        var totalElement = hits.GetPropertyIgnoreCase("total");
        var total = totalElement.ValueKind == JsonValueKind.Object ? totalElement.GetPropertyIgnoreCase("value").GetInt32() : totalElement.GetInt32();
        var records = hits
            .GetPropertyIgnoreCase("hits")
            .EnumerateArray()
            .Select(hit => JsonSerializer.Deserialize<SystemLogItem>(hit.GetPropertyIgnoreCase("_source").GetRawText(), JsonParsing.Options)
                           ?? throw new InvalidOperationException("Invalid Elasticsearch log document")
            )
            .ToList();
        return (records, total);
    }
}