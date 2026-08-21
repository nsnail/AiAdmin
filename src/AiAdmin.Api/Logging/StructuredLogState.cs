using System.Collections;

namespace AiAdmin.Api.Logging;

/// <summary>
///     保存可供日志提供程序解析的结构化日志状态
/// </summary>
internal sealed class StructuredLogState : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly KeyValuePair<string, object?>[] _fields;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StructuredLogState" /> class
    ///     初始化结构化日志状态
    /// </summary>
    /// <param name="template">日志文本模板</param>
    /// <param name="fields">日志字段</param>
    public StructuredLogState(
        string template
        , IReadOnlyDictionary<string, object?> fields
    ) {
        _fields = fields.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value)).ToArray();
        Message = string.Join("; ", new[] { template }.Concat(_fields.Select(x => $"{x.Key}={x.Value}")));
    }

    /// <summary>
    ///     获取结构化字段数量
    /// </summary>
    public int Count => _fields.Length;

    /// <summary>
    ///     获取格式化后的日志消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     获取指定位置的结构化字段
    /// </summary>
    /// <param name="index">字段索引</param>
    /// <returns>结构化字段</returns>
    public KeyValuePair<string, object?> this[int index] => _fields[index];

    /// <summary>
    ///     获取字段枚举器
    /// </summary>
    /// <returns>字段枚举器</returns>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() {
        return ((IEnumerable<KeyValuePair<string, object?>>)_fields).GetEnumerator();
    }

    /// <summary>
    ///     获取字段枚举器
    /// </summary>
    /// <returns>字段枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator() {
        return _fields.GetEnumerator();
    }
}