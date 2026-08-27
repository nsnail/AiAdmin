using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Services;

/// <summary>
///     统一将 API 的 DateTimeOffset 输入输出转换为 UTC 偏移
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>
    ///     读取带时区的时间并转换为 UTC 偏移
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>UTC 偏移时间</returns>
    /// <exception cref="JsonException">输入不是有效时间时抛出</exception>
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader
        , Type typeToConvert
        , JsonSerializerOptions options
    ) {
        var value = reader.GetString() ?? throw new JsonException("UTC date time value is required.");
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result.ToUniversalTime()
            : throw new JsonException("UTC date time value is invalid.");
    }

    /// <summary>
    ///     将 DateTimeOffset 按 UTC ISO 8601 格式写入 JSON
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">待写入时间</param>
    /// <param name="options">序列化选项</param>
    public override void Write(
        Utf8JsonWriter writer
        , DateTimeOffset value
        , JsonSerializerOptions options
    ) {
        writer.WriteStringValue(value.ToUniversalTime());
    }
}