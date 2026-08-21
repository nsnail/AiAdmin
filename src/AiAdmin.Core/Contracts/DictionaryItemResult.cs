namespace AiAdmin.Api.Contracts;

/// <summary>
///     字典内容列表项
/// </summary>
public sealed record DictionaryItemResult(
    long Id
    , DateTimeOffset CreatedAt
    , long CategoryId
    , string Value
    , string Label
    , int Sort
    , bool IsEnabled
    , string Remark);