namespace AiAdmin.Api.Contracts;

/// <summary>
///     字典内容列表项
/// </summary>
/// <param name="Id">字典项标识</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="CategoryId">分类标识</param>
/// <param name="Value">字典值</param>
/// <param name="Label">显示标签</param>
/// <param name="Sort">排序值</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="Remark">备注</param>
public sealed record DictionaryItemResult(
    long Id
    , DateTimeOffset CreatedAt
    , long CategoryId
    , string Value
    , string Label
    , int Sort
    , bool IsEnabled
    , string Remark);