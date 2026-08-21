// 定义接口管理页面使用的查询、同步和授权请求响应模型。
namespace AiAdmin.Api.Contracts;

/// <summary>
///     接口同步统计结果
/// </summary>
public sealed record ApiSyncResult(int Added, int Updated, int Deleted, int Total);