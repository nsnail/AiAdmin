using System.Text.Json;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

// 提供角色维护及角色菜单、接口权限映射管理。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     角色和权限管理控制器
/// </summary>
[ApiController]
[ApiDescription("Role management")]
[Authorize]
[Route("api/role")]
public sealed class RolesController(AppDbContext db, ApiPermissionCache permissionCache) : ControllerBase
{
    /// <summary>
    ///     查询角色已授权的接口主键
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <returns>接口主键集合</returns>
    [HttpGet("{id:long}/apis")]
    [ApiDescription("Query role API permissions")]
    public async Task<ActionResult<ApiResponse<long[]>>> ApisAsync(long id) {
        if (!await db.Roles.AnyAsync(x => x.Id == id).ConfigureAwait(false)) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var apiIds = await db.RoleApis.AsNoTracking().Where(x => x.RoleId == id).Select(x => x.ApiEndpointId).ToArrayAsync().ConfigureAwait(false);
        return Ok(ApiResponse<long[]>.Ok(apiIds));
    }

    /// <summary>
    ///     创建角色
    /// </summary>
    /// <param name="request">角色保存请求</param>
    /// <returns>创建后的角色</returns>
    [HttpPost]
    [ApiDescription("Create role")]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> CreateAsync(SaveRoleRequest request) {
        var code = request.RoleCode.Trim();
        var dataScope = request.DataScope.Trim();
        if (!RoleDataScope.IsValid(dataScope)) {
            return BadRequest(new ApiResponse<object>(400, "Invalid data scope", null));
        }

        if (await db.Roles.AnyAsync(x => x.Code == code).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        var role = new Role
        {
            Name = request.RoleName.Trim()
            , Code = code
            , Description = request.Description.Trim()
            , DataScope = dataScope
            , IsEnabled = request.Enabled
        };
        _ = await db.Roles.AddAsync(role).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(role), "Role created"));
    }

    /// <summary>
    ///     删除角色
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete role")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var role = await db.Roles.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        if (role.UserRoles.Count > 0) {
            return BadRequest(new ApiResponse<object>(400, "Role is assigned to users", null));
        }

        _ = db.Roles.Remove(role);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<object>.Ok(new { }, "Role deleted"));
    }

    /// <summary>
    ///     查询角色列表筛选字段元数据
    /// </summary>
    /// <returns>角色筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query role filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<Role>()));
    }

    /// <summary>
    ///     分页查询角色
    /// </summary>
    /// <param name="request">包含动态筛选和分页信息的请求体</param>
    /// <returns>角色分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query role list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<RoleListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var current = request.Current;
        var size = request.Size;
        var query = db.Roles.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);

        var total = await query.CountAsync().ConfigureAwait(false);
        var sortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["roleId"] = nameof(Role.Id)
            , ["roleName"] = nameof(Role.Name)
            , ["roleCode"] = nameof(Role.Code)
            , ["enabled"] = nameof(Role.IsEnabled)
            , ["createTime"] = nameof(Role.CreatedAt)
        };
        var roles = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(Role.CreatedAt), true, sortAliases)
            .Skip((current - 1) * size)
            .Take(size)
            .ToListAsync()
            .ConfigureAwait(false);
        var items = roles.ConvertAll(ToListItem);
        return Ok(ApiResponse<PagedResponse<RoleListItem>>.Ok(new PagedResponse<RoleListItem>(items, current, size, total)));
    }

    /// <summary>
    ///     查询角色已授权的菜单树
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <returns>角色菜单树</returns>
    [HttpGet("{id:long}/menus")]
    [ApiDescription("Query role menu permissions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> MenusAsync(long id) {
        if (!await db.Roles.AnyAsync(x => x.Id == id).ConfigureAwait(false)) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var rows = await db
            .RoleMenus.AsNoTracking()
            .Where(x => x.RoleId == id)
            .Include(x => x.Menu)
            .Select(x => x.Menu)
            .OrderBy(x => x.Sort)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildTree(rows)));
    }

    /// <summary>
    ///     保存角色接口权限
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <param name="request">接口授权请求</param>
    /// <returns>保存结果</returns>
    [HttpPut("{id:long}/apis")]
    [ApiDescription("Save role API permissions")]
    public async Task<ActionResult<ApiResponse<object>>> SaveApisAsync(
        long id
        , SaveRoleApisRequest request
    ) {
        // 替换角色接口映射并使权限缓存立即失效。
        var role = await db.Roles.Include(x => x.RoleApis).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var requestedIds = request.ApiIds.Distinct().ToArray();
        var endpoints = await db.ApiEndpoints.Where(x => requestedIds.Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        if (endpoints.Count != requestedIds.Length) {
            return BadRequest(new ApiResponse<object>(400, "Invalid API endpoint", null));
        }

        db.RoleApis.RemoveRange(role.RoleApis);
        role.RoleApis = [.. endpoints.Select(endpoint => new RoleApi { Role = role, ApiEndpoint = endpoint })];
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<object>.Ok(new { }, "API permissions saved"));
    }

    /// <summary>
    ///     保存角色菜单权限
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <param name="request">菜单授权请求</param>
    /// <returns>保存结果</returns>
    [HttpPut("{id:long}/menus")]
    [ApiDescription("Save role menu permissions")]
    public async Task<ActionResult<ApiResponse<object>>> SaveMenusAsync(
        long id
        , SaveRoleMenusRequest request
    ) {
        var role = await db.Roles.Include(x => x.RoleMenus).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        db.RoleMenus.RemoveRange(role.RoleMenus);
        var menus = await db.Menus.Where(x => request.MenuIds.Distinct().Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        role.RoleMenus = [.. menus.Select(menu => new RoleMenu { Role = role, Menu = menu })];
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Menu permissions saved"));
    }

    /// <summary>
    ///     更新角色
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <param name="request">角色保存请求</param>
    /// <returns>更新后的角色</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update role")]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> UpdateAsync(
        long id
        , SaveRoleRequest request
    ) {
        var role = await db.Roles.FindAsync(id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var code = request.RoleCode.Trim();
        var dataScope = request.DataScope.Trim();
        if (!RoleDataScope.IsValid(dataScope)) {
            return BadRequest(new ApiResponse<object>(400, "Invalid data scope", null));
        }

        if (await db.Roles.AnyAsync(x => x.Code == code && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        role.Name = request.RoleName.Trim();
        role.Code = code;
        role.Description = request.Description.Trim();
        role.DataScope = dataScope;
        role.IsEnabled = request.Enabled;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(role), "Role updated"));
    }

    private static IReadOnlyList<MenuItemResult> BuildTree(IReadOnlyList<Menu> rows) {
        var nodes = rows.ToDictionary(
            x => x.Name
            , x => new MenuItemResult(
                x.Id, ServerTime.ToOffset(x.CreatedAt), x.Name, x.Path, x.Component, x.ParentName, x.Sort, x.IsEnabled, ParseMeta(x.MetaJson), []
            ), StringComparer.Ordinal
        );
        return BuildChildren(string.Empty);

        IReadOnlyList<MenuItemResult> BuildChildren(string parentName) {
            return
            [
                .. nodes
                    .Values.Where(x => x.ParentName == parentName)
                    .OrderBy(x => x.Sort)
                    .Select(x => x with { Children = BuildChildren(x.Name) })
            ];
        }
    }

    private static JsonElement ParseMeta(string json) {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static RoleListItem ToListItem(Role role) {
        return new RoleListItem(role.Id, role.Name, role.Code, role.Description, role.DataScope, role.IsEnabled, ServerTime.ToOffset(role.CreatedAt));
    }
}