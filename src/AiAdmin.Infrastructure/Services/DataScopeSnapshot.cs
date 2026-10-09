namespace AiAdmin.Api.Services;

/// <summary>
///     用户数据权限快照
/// </summary>
/// <param name="UserId">用户主键</param>
/// <param name="HasAllData">是否拥有全部数据权限</param>
/// <param name="HasSelfData">是否允许访问本人数据</param>
/// <param name="DepartmentIds">可访问部门主键</param>
/// <param name="DefaultOwnerDepartmentId">默认所属部门主键</param>
public sealed record DataScopeSnapshot(long UserId, bool HasAllData, bool HasSelfData, long[] DepartmentIds, long DefaultOwnerDepartmentId);