namespace AiAdmin.Api.Contracts;

/// <summary>
///     字典目录树节点
/// </summary>
public sealed record DictionaryCategoryResult(
    long Id
    , DateTimeOffset CreatedAt
    , string Code
    , string Name
    , long? ParentId
    , int Sort
    , bool IsEnabled
    , IReadOnlyList<DictionaryCategoryResult> Children);