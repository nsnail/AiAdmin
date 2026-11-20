using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiAdmin.Api.Logging;

/// <summary>
///     日志脱敏工具：把请求头和请求体、响应体中的密码、令牌、密钥等敏感值替换为 ***，避免凭据写入日志和 Elasticsearch
/// </summary>
public static partial class SensitiveDataMasker
{
    /// <summary>
    ///     脱敏后的占位值
    /// </summary>
    public const string MASK = "***";

    /// <summary>
    ///     名称包含这些片段（忽略大小写，忽略下划线和连字符）的字段视为敏感字段
    /// </summary>
    private static readonly string[] _sensitiveKeyParts =
    [
        "password", "passwd", "secret", "token", "apikey", "privatekey", "sessiondata", "verificationcode", "authorization", "cookie"
    ];

    /// <summary>
    ///     序列化脱敏后的 JSON 时使用的选项，保留中文等非 ASCII 字符不被转义
    /// </summary>
    private static readonly JsonSerializerOptions _writeOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    ///     判断字段名是否为敏感字段
    /// </summary>
    /// <param name="key">字段名或消息头名称</param>
    /// <returns>敏感字段返回 true</returns>
    public static bool IsSensitiveKey(string key) {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return _sensitiveKeyParts.Any(part => normalized.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     按内容类型对请求体或响应体脱敏，仅处理 JSON 和表单，其他类型原样返回
    /// </summary>
    /// <param name="body">请求体或响应体文本</param>
    /// <param name="contentType">HTTP 内容类型</param>
    /// <returns>脱敏后的文本</returns>
    public static string MaskBody(
        string? body
        , string? contentType
    ) {
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(contentType)) {
            return body ?? string.Empty;
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) {
            return MaskJson(body);
        }

        return contentType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ? FormRegex().Replace(body, MaskFormPair) : body;
    }

    /// <summary>
    ///     把消息头序列化为 JSON，敏感消息头的值以 *** 替代
    /// </summary>
    /// <param name="headers">消息头名称与值</param>
    /// <returns>脱敏后的 JSON 文本</returns>
    public static string MaskHeaders(IEnumerable<KeyValuePair<string, string>> headers) {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in headers) {
            values[name] = IsSensitiveKey(name) ? MASK : value;
        }

        return JsonSerializer.Serialize(values);
    }

    /// <summary>
    ///     匹配表单中的键值对
    /// </summary>
    /// <returns>键值对匹配正则</returns>
    [GeneratedRegex("(?<key>[^=&]+)=(?<value>[^&]*)", RegexOptions.None, 1000)]
    private static partial Regex FormRegex();

    /// <summary>
    ///     匹配 JSON 文本中字符串类型的键值对，用于 JSON 无法解析（如被截断）时的兜底脱敏
    /// </summary>
    /// <returns>键值对匹配正则</returns>
    [GeneratedRegex(@"(""(?<key>[^""\\]+)""\s*:\s*)""(?:[^""\\]|\\.)*""", RegexOptions.None, 1000)]
    private static partial Regex JsonStringPairRegex();

    /// <summary>
    ///     对正则匹配到的表单键值对脱敏
    /// </summary>
    /// <param name="match">匹配结果</param>
    /// <returns>替换后的文本</returns>
    private static string MaskFormPair(Match match) {
        var key = match.Groups["key"].Value;
        return IsSensitiveKey(Uri.UnescapeDataString(key)) ? $"{key}={MASK}" : match.Value;
    }

    /// <summary>
    ///     对 JSON 文本脱敏，解析失败时退回到正则兜底
    /// </summary>
    /// <param name="body">JSON 文本</param>
    /// <returns>脱敏后的文本</returns>
    private static string MaskJson(string body) {
        try {
            var node = JsonNode.Parse(body);
            if (node is null) {
                return body;
            }

            MaskNode(node);
            return node.ToJsonString(_writeOptions);
        }
        catch (JsonException) {
            return JsonStringPairRegex().Replace(body, MaskJsonPair);
        }
    }

    /// <summary>
    ///     对正则匹配到的 JSON 键值对脱敏
    /// </summary>
    /// <param name="match">匹配结果</param>
    /// <returns>替换后的文本</returns>
    private static string MaskJsonPair(Match match) {
        return IsSensitiveKey(match.Groups["key"].Value) ? $"{match.Groups[1].Value}\"{MASK}\"" : match.Value;
    }

    /// <summary>
    ///     递归遍历 JSON 节点，把敏感字段的值替换为占位值
    /// </summary>
    /// <param name="node">JSON 节点</param>
    private static void MaskNode(JsonNode? node) {
        switch (node) {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToList()) {
                    if (obj[key] is not null && IsSensitiveKey(key)) {
                        obj[key] = MASK;
                    }
                    else {
                        MaskNode(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array) {
                    MaskNode(item);
                }

                break;
        }
    }
}