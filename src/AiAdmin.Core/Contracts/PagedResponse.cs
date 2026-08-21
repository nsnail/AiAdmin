namespace AiAdmin.Api.Contracts;

/// <summary>
///     分页查询响应
/// </summary>
/// <typeparam name="T">记录类型</typeparam>
public sealed record PagedResponse<T>(IReadOnlyList<T> Records, int Current, int Size, int Total);