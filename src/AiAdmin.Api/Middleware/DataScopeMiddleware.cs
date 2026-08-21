using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Middleware;

/// <summary>
///     初始化当前请求的数据权限范围
/// </summary>
public sealed class DataScopeMiddleware(RequestDelegate next)
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
        if (context.User.Identity?.IsAuthenticated != true
            || !long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var userId)) {
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
        var hasAllData = scopes.Contains(RoleDataScope.ALL);
        var allowedDepartments = new HashSet<long>();
        if (scopes.Contains(RoleDataScope.DEPARTMENT)) {
            allowedDepartments.UnionWith(directDepartmentIds);
        }

        if (scopes.Contains(RoleDataScope.DEPARTMENT_AND_CHILDREN)) {
            var departments = await db
                .Departments.IgnoreQueryFilters()
                .AsNoTracking()
                .Select(x => new { x.Id, x.ParentId })
                .ToListAsync(context.RequestAborted)
                .ConfigureAwait(false);
            var children = departments.ToLookup(x => x.ParentId);
            var pending = new Queue<long>(directDepartmentIds);
            while (pending.TryDequeue(out var departmentId)) {
                if (!allowedDepartments.Add(departmentId)) {
                    continue;
                }

                foreach (var child in children[departmentId]) {
                    pending.Enqueue(child.Id);
                }
            }
        }

        var personalDepartmentId = await db
            .Departments.AsNoTracking()
            .Where(x => x.Code == $"USER_{userId}")
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(context.RequestAborted)
            .ConfigureAwait(false);

        dataScope.Initialize(userId, hasAllData, scopes.Contains(RoleDataScope.SELF), allowedDepartments, personalDepartmentId ?? 0);
        await next(context).ConfigureAwait(false);
    }
}