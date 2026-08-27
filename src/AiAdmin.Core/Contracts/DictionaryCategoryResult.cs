namespace AiAdmin.Api.Contracts;

/// <summary>
///     字典目录树节点
/// </summary>
/// <param name="Id">分类标识</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="Code">分类编码</param>
/// <param name="Name">分类名称</param>
/// <param name="ParentId">父分类标识</param>
/// <param name="Sort">排序值</param>
/// <param name="Children">子分类</param>
public sealed record DictionaryCategoryResult(
    long Id
    , DateTimeOffset CreatedAt
    , string Code
    , string Name
    , long? ParentId
    , int Sort
    , IReadOnlyList<DictionaryCategoryResult> Children);