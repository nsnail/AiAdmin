// 保存当前请求的数据权限范围，供数据库上下文统一执行过滤和写入校验
using AiAdmin.Api.Models;

namespace AiAdmin.Api.Services;

/// <summary>
///     当前请求的数据权限上下文
/// </summary>
public sealed class DataScopeContext
{
    /// <summary>
    ///     新增数据时默认写入的所有者部门主键
    /// </summary>
    public long DefaultOwnerDepartmentId { get; private set; }

    /// <summary>
    ///     当前用户可操作的所有者部门主键集合
    /// </summary>
    public IReadOnlySet<long> DepartmentIds { get; private set; } = new HashSet<long>();

    /// <summary>
    ///     是否拥有全部数据权限
    /// </summary>
    public bool HasAllData { get; private set; }

    /// <summary>
    ///     是否允许访问本人数据
    /// </summary>
    public bool HasSelfData { get; private set; }

    /// <summary>
    ///     是否已为当前请求完成初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     当前登录用户主键
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    ///     判断是否可操作指定所有者数据
    /// </summary>
    /// <param name="owner">目标数据所有者</param>
    /// <returns>可操作时返回 true</returns>
    public bool CanAccess(IOwner owner) {
        return HasAllData || (HasSelfData && owner.OwnerId == UserId) || DepartmentIds.Contains(owner.OwnerDepartmentId);
    }

    /// <summary>
    ///     初始化当前请求的数据权限范围
    /// </summary>
    /// <param name="userId">当前登录用户主键</param>
    /// <param name="hasAllData">是否拥有全部数据权限</param>
    /// <param name="hasSelfData">是否允许访问本人数据</param>
    /// <param name="departmentIds">可操作部门主键集合</param>
    /// <param name="defaultOwnerDepartmentId">新增数据默认所属部门主键</param>
    public void Initialize(
        long userId
        , bool hasAllData
        , bool hasSelfData
        , IReadOnlySet<long> departmentIds
        , long defaultOwnerDepartmentId
    ) {
        UserId = userId;
        HasAllData = hasAllData;
        HasSelfData = hasSelfData;
        DepartmentIds = departmentIds;
        DefaultOwnerDepartmentId = defaultOwnerDepartmentId;
        IsInitialized = true;
    }
}