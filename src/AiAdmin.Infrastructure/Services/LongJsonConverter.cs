// 将 Int64 API 值序列化为字符串，避免浏览器解析随机主键时发生精度丢失
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Services;

/// <summary>
///     支持字符串和数字输入并始终输出字符串的 Int64 JSON 转换器
/// </summary>
public sealed class LongJsonConverter : JsonConverter<long>
{
    /// <summary>
    ///     从 JSON 字符串或数字读取 Int64 值
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化配置</param>
    /// <returns>读取到的 Int64 值</returns>
    /// <exception cref="JsonException">JSON 值不是有效的 Int64 字符串或数字时抛出</exception>
    public override long Read(
        ref Utf8JsonReader reader
        , Type typeToConvert
        , JsonSerializerOptions options
    ) {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out var number) => number
            , JsonTokenType.String when long.TryParse(
                reader.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var textNumber
            ) => textNumber
            , _ => throw new JsonException("Expected an Int64 value encoded as a JSON string or number")
        };
    }

    /// <summary>
    ///     将 Int64 值写为 JSON 字符串
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">待写入值</param>
    /// <param name="options">序列化配置</param>
    public override void Write(
        Utf8JsonWriter writer
        , long value
        , JsonSerializerOptions options
    ) {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}