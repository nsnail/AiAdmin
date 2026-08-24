using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAdmin.Api.Services;

/// <summary>
///     统一将 API 的 DateTime 输入输出转换为 UTC 时间
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary>
    ///     读取带时区或 UTC 的时间并转换为 UTC DateTime
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>UTC 时间</returns>
    /// <exception cref="JsonException">输入不是有效的 UTC 时间时抛出</exception>
    public override DateTime Read(
        ref Utf8JsonReader reader
        , Type typeToConvert
        , JsonSerializerOptions options
    ) {
        var value = reader.GetString() ?? throw new JsonException("UTC date time value is required.");
        var result = DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : throw new JsonException("UTC date time value is invalid.");

        return result.UtcDateTime;
    }

    /// <summary>
    ///     将 DateTime 按 UTC ISO 8601 格式写入 JSON
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">待写入时间</param>
    /// <param name="options">序列化选项</param>
    public override void Write(
        Utf8JsonWriter writer
        , DateTime value
        , JsonSerializerOptions options
    ) {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc);
    }
}