namespace AiAdmin.Api.Contracts;

/// <summary>
///     分页查询响应
/// </summary>
/// <typeparam name="T">记录类型</typeparam>
/// <param name="Records">记录列表</param>
/// <param name="Current">当前页码</param>
/// <param name="Size">每页记录数</param>
/// <param name="Total">总记录数</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Records, int Current, int Size, int Total);