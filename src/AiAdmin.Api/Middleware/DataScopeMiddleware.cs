using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     初始化当前请求的数据权限范围
/// </summary>
/// <param name="next">后续请求处理委托</param>
/// <param name="cache">数据权限缓存</param>
public sealed class DataScopeMiddleware(RequestDelegate next, DataScopeCache cache)
{
    /// <summary>
    ///     根据用户角色和绑定部门计算数据权限范围
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="db">应用数据库上下文</param>
    /// <param name="dataScope">当前请求的数据权限上下文</param>
    /// <returns>异步请求处理任务</returns>
    public async Task InvokeAsync(
        HttpContext context
        , AppDbContext db
        , DataScopeContext dataScope
    ) {
        if (!TryGetUserId(context.User, out var userId)) {
            await next(context).ConfigureAwait(false);
            return;
        }

        DataScopeSnapshot? cached = null;
        try {
            cached = await cache.GetAsync(userId, context.RequestAborted).ConfigureAwait(false);
        }
        catch (RedisException) {
            // Redis 不可用时回退数据库计算，不能阻断业务请求
        }

        if (cached is not null) {
            dataScope.Initialize(
                cached.UserId, cached.HasAllData, cached.HasSelfData, cached.DepartmentIds.ToHashSet(), cached.DefaultOwnerDepartmentId
            );
            await next(context).ConfigureAwait(false);
            return;
        }

        var scopes = await db
            .UserRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.Role.IsEnabled)
            .Select(x => x.Role.DataScope)
            .ToListAsync(context.RequestAborted)
            .ConfigureAwait(false);
        var directDepartmentIds = await db
            .UserDepartments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.DepartmentId)
            .ToListAsync(context.RequestAborted)
            .ConfigureAwait(false);
        var allowedDepartments = await GetAllowedDepartmentIdsAsync(db, scopes, directDepartmentIds, context.RequestAborted).ConfigureAwait(false);
        var personalDepartmentId = await GetPersonalDepartmentIdAsync(db, userId, context.RequestAborted).ConfigureAwait(false);

        dataScope.Initialize(
            userId, scopes.Contains(RoleDataScope.All), scopes.Contains(RoleDataScope.Self), allowedDepartments, personalDepartmentId
        );
        try {
            await cache
                .SetAsync(
                    new DataScopeSnapshot(
                        userId, scopes.Contains(RoleDataScope.All), scopes.Contains(RoleDataScope.Self), [.. allowedDepartments], personalDepartmentId
                    )
                )
                .ConfigureAwait(false);
        }
        catch (RedisException) {
            // Redis 写入失败仅影响缓存，不影响本次请求
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    ///     将指定部门及其全部子部门加入允许访问集合
    /// </summary>
    /// <param name="allowedDepartmentIds">允许访问的部门主键集合</param>
    /// <param name="rootDepartmentIds">部门树根节点主键</param>
    /// <param name="children">按父部门主键分组的子部门索引</param>
    private static void AddDepartmentTrees(
        HashSet<long> allowedDepartmentIds
        , IEnumerable<long> rootDepartmentIds
        , ILookup<long?, long> children
    ) {
        var pending = new Queue<long>(rootDepartmentIds);
        var visited = new HashSet<long>();
        while (pending.TryDequeue(out var departmentId)) {
            if (!visited.Add(departmentId)) {
                continue;
            }

            _ = allowedDepartmentIds.Add(departmentId);
            foreach (var childId in children[departmentId]) {
                pending.Enqueue(childId);
            }
        }
    }

    /// <summary>
    ///     计算角色数据范围允许访问的部门主键
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <param name="scopes">用户角色数据范围</param>
    /// <param name="directDepartmentIds">用户直接绑定的部门主键</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>允许访问的部门主键集合</returns>
    private static async Task<HashSet<long>> GetAllowedDepartmentIdsAsync(
        AppDbContext db
        , IReadOnlyCollection<RoleDataScope> scopes
        , IReadOnlyCollection<long> directDepartmentIds
        , CancellationToken cancellationToken
    ) {
        var allowedDepartmentIds = scopes.Contains(RoleDataScope.Department) ? directDepartmentIds.ToHashSet() : [];
        if (!scopes.Contains(RoleDataScope.DepartmentAndChildren)) {
            return allowedDepartmentIds;
        }

        var departments = await db
            .Departments.IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        AddDepartmentTrees(allowedDepartmentIds, directDepartmentIds, departments.ToLookup(x => x.ParentId, x => x.Id));
        return allowedDepartmentIds;
    }

    /// <summary>
    ///     查询用户个人部门主键
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <param name="userId">用户主键</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>个人部门主键，不存在时返回零</returns>
    private static async Task<long> GetPersonalDepartmentIdAsync(
        AppDbContext db
        , long userId
        , CancellationToken cancellationToken
    ) {
        return await db
                   .Departments.AsNoTracking()
                   .Where(x => x.Code == $"USER_{userId}")
                   .Select(x => (long?)x.Id)
                   .SingleOrDefaultAsync(cancellationToken)
                   .ConfigureAwait(false)
               ?? 0;
    }

    /// <summary>
    ///     从已认证用户声明中读取用户主键
    /// </summary>
    /// <param name="user">当前请求用户</param>
    /// <param name="userId">解析出的用户主键</param>
    /// <returns>用户已认证且主键有效时返回 true</returns>
    private static bool TryGetUserId(
        ClaimsPrincipal user
        , out long userId
    ) {
        userId = 0;
        return user.Identity?.IsAuthenticated == true
               && long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out userId);
    }
}