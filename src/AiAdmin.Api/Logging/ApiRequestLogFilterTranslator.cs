using System.Globalization;
using System.Net;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Logging;

/// <summary>
///     把请求日志列表上以可读形式提交的筛选条件翻译成表中实际存储的整数：IP 文本转 IPv4 整数，
///     其余字段（方法、状态码、用户、路径 CRC32）本身就是数值，保持不变
/// </summary>
public static class ApiRequestLogFilterTranslator
{
    private static readonly string[] _ipFields = ["ClientIp", "ServerIp"];

    /// <summary>
    ///     递归翻译动态筛选树，返回新的筛选树，不修改入参
    /// </summary>
    /// <param name="filter">原动态筛选节点</param>
    /// <returns>翻译后的动态筛选节点，入参为空时返回 null</returns>
    /// <exception cref="DynamicFilterValidationException">IP 文本无法转换为 IPv4 时引发</exception>
    public static DynamicFilter? Translate(DynamicFilter? filter) {
        if (filter is null) {
            return null;
        }

        var isIpField = filter.Field is not null && _ipFields.Contains(filter.Field.Trim(), StringComparer.OrdinalIgnoreCase);
        return new DynamicFilter
        {
            Field = filter.Field
            , Operator = filter.Operator
            , Logic = filter.Logic
            , Filters = [.. filter.Filters.Select(Translate).OfType<DynamicFilter>()]
            , NestedDynamicFilter = Translate(filter.NestedDynamicFilter)
            , Value = isIpField && filter.Value is { } value ? TranslateIpValue(value) : filter.Value
        };
    }

    /// <summary>
    ///     翻译 IP 筛选值，支持单个文本、数字以及集合操作符使用的文本数组
    /// </summary>
    /// <param name="value">原筛选值</param>
    /// <returns>整数筛选值</returns>
    /// <exception cref="DynamicFilterValidationException">IP 文本无法转换为 IPv4 时引发</exception>
    private static JsonElement TranslateIpValue(JsonElement value) {
        return value.ValueKind switch
        {
            JsonValueKind.String => ToElement(ParseIp(value.GetString()))
            , JsonValueKind.Array => ToElement(
                value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? ParseIp(item.GetString()) : item.GetInt32()).ToArray()
            )
            , _ => value
        };
    }

    /// <summary>
    ///     把 IPv4 文本转换为 32 位整数
    /// </summary>
    /// <param name="text">IP 文本</param>
    /// <returns>32 位整数</returns>
    /// <exception cref="DynamicFilterValidationException">文本不是合法的 IPv4 地址时引发</exception>
    private static int ParseIp(string? text) {
        var trimmed = text?.Trim();

        // 纯数字文本按整数原值处理，便于从详情里复制整数值精确查询
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && trimmed?.Contains('.', StringComparison.Ordinal) == false) {
            return number;
        }

        return IPAddress.TryParse(trimmed, out var address) && ApiRequestLogMapper.ToIpv4(address) is { } ip
            ? ip
            : throw new DynamicFilterValidationException($"IP address '{text}' is not a valid IPv4 address.");
    }

    /// <summary>
    ///     把对象序列化后重新解析为独立的 JsonElement
    /// </summary>
    /// <param name="value">待转换的值</param>
    /// <returns>JSON 元素</returns>
    private static JsonElement ToElement(object value) {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }
}