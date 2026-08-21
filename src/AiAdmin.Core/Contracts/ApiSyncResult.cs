namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口同步统计结果
/// </summary>
public sealed record ApiSyncResult(int Added, int Updated, int Deleted, int Total);