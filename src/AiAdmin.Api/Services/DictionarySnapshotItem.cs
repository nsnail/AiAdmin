namespace AiAdmin.Api.Services;

/// <summary>
///     Redis 字典快照中的字典项
/// </summary>
public sealed class DictionarySnapshotItem
{
    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    ///     字典标签
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    ///     备注
    /// </summary>
    public required string Remark { get; init; }

    /// <summary>
    ///     排序值
    /// </summary>
    public int Sort { get; init; }

    /// <summary>
    ///     字典值
    /// </summary>
    public required string Value { get; init; }
}