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

namespace AiAdmin.Api.Controllers;

/// <summary>
///     角色和权限管理控制器
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="permissionCache">接口权限缓存</param>
/// <param name="exportLimitService">列表导出上限服务</param>
[ApiController]
[ApiDescription("Role management")]
[Authorize]
[Route("api/role")]
public sealed class RolesController(AppDbContext db, ApiPermissionCache permissionCache, ExportLimitService exportLimitService) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> _sortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["roleId"] = nameof(Role.Id)
        , ["roleName"] = nameof(Role.Name)
        , ["roleCode"] = nameof(Role.Code)
        , ["enabled"] = nameof(Role.IsEnabled)
        , ["createTime"] = nameof(Role.CreatedAt)
        , ["updateTime"] = nameof(Role.UpdatedAt)
    };

    /// <summary>
    ///     查询角色已授权的接口主键
    /// </summary>
    /// <param name="id">角色主键</param>
    /// <returns>接口主键集合</returns>
    [HttpGet("apis")]
    [ApiDescription("Query role API permissions")]
    public async Task<ActionResult<ApiResponse<long[]>>> ApisAsync([FromQuery] long id) {
        if (!await db.Roles.AnyAsync(x => x.Id == id).ConfigureAwait(false)) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var apiIds = await db.RoleApis.AsNoTracking().Where(x => x.RoleId == id).Select(x => x.ApiEndpointId).ToArrayAsync().ConfigureAwait(false);
        return Ok(ApiResponse<long[]>.Ok(apiIds));
    }

    /// <summary>
    ///     复制角色及其菜单和接口权限
    /// </summary>
    /// <param name="request">源角色标识请求</param>
    /// <returns>复制后的角色</returns>
    [HttpPost("copy")]
    [ApiDescription("Copy role")]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> CopyAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
        var source = await db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (source is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var code = await CreateCopyCodeAsync(source.Code).ConfigureAwait(false);
        var menuIds = await db.RoleMenus.AsNoTracking().Where(x => x.RoleId == id).Select(x => x.MenuId).ToArrayAsync().ConfigureAwait(false);
        var apiIds = await db.RoleApis.AsNoTracking().Where(x => x.RoleId == id).Select(x => x.ApiEndpointId).ToArrayAsync().ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);
        var copy = new Role
        {
            Name = $"{source.Name}_COPY"
            , Code = code
            , Description = source.Description
            , DataScope = source.DataScope
            , IsEnabled = source.IsEnabled
            , RoleMenus = [.. menuIds.Select(menuId => new RoleMenu { MenuId = menuId })]
            , RoleApis = [.. apiIds.Select(apiId => new RoleApi { ApiEndpointId = apiId })]
        };
        _ = await db.Roles.AddAsync(copy).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(copy), "Role copied"));
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
        if (await db.Roles.AnyAsync(x => x.Code == code).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        var role = new Role
        {
            Name = request.RoleName.Trim()
            , Code = code
            , Description = request.Description.Trim()
            , DataScope = request.DataScope
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
    /// <param name="request">角色标识请求</param>
    /// <returns>删除结果</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete role")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
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
    ///     按当前筛选和排序条件导出角色数据
    /// </summary>
    /// <param name="request">角色导出请求</param>
    /// <returns>受系统设置条数限制的角色数据</returns>
    [HttpPost("export")]
    [ApiDescription("Export role data")]
    public async Task<ActionResult<ApiResponse<RoleExportResult>>> ExportAsync([FromBody] RoleExportRequest request) {
        var query = BuildListQuery(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var limit = await exportLimitService.GetLimitAsync().ConfigureAwait(false);
        var roles = await ApplyListSort(query, request.SortField, request.SortOrder).Take(limit).ToListAsync().ConfigureAwait(false);
        return Ok(ApiResponse<RoleExportResult>.Ok(new RoleExportResult(roles.ConvertAll(ToListItem), limit, total)));
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
    ///     查询当前角色筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>可用于进一步筛选的字段分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query role filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService.GetGroupsAsync(db.Roles.AsNoTracking(), request.DynamicFilter).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
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
        var query = BuildListQuery(request.DynamicFilter);

        var total = await query.CountAsync().ConfigureAwait(false);
        var roles = await ApplyListSort(query, request.SortField, request.SortOrder)
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
    [HttpGet("menus")]
    [ApiDescription("Query role menu permissions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> MenusAsync([FromQuery] long id) {
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
    /// <param name="request">接口授权请求</param>
    /// <returns>保存结果</returns>
    [HttpPost("apis")]
    [ApiDescription("Save role API permissions")]
    public async Task<ActionResult<ApiResponse<object>>> SaveApisAsync([FromBody] SaveRoleApisRequest request) {
        var id = request.Id;

        // 替换角色接口映射并使权限缓存立即失效。
        var role = await db.Roles.Include(x => x.RoleApis).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var requestedIds = request.ApiIds.Distinct().ToArray();
        var endpoints = await db.ApiEndpoints.Where(x => Enumerable.Contains(requestedIds, x.Id)).ToListAsync().ConfigureAwait(false);
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
    /// <param name="request">菜单授权请求</param>
    /// <returns>保存结果</returns>
    [HttpPost("menus")]
    [ApiDescription("Save role menu permissions")]
    public async Task<ActionResult<ApiResponse<object>>> SaveMenusAsync([FromBody] SaveRoleMenusRequest request) {
        var id = request.Id;
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
    /// <param name="request">角色保存请求</param>
    /// <returns>更新后的角色</returns>
    [HttpPost("update")]
    [ApiDescription("Update role")]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> UpdateAsync([FromBody] SaveRoleRequest request) {
        var id = request.Id.GetValueOrDefault();
        var role = await db.Roles.FindAsync(id).ConfigureAwait(false);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var code = request.RoleCode.Trim();
        if (await db.Roles.AnyAsync(x => x.Code == code && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        role.Name = request.RoleName.Trim();
        role.Code = code;
        role.Description = request.Description.Trim();
        role.DataScope = request.DataScope;
        role.IsEnabled = request.Enabled;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(role), "Role updated"));
    }

    /// <summary>
    ///     将角色查询应用统一的服务端排序
    /// </summary>
    /// <param name="query">已应用动态筛选的角色查询</param>
    /// <param name="sortField">客户端排序字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <returns>已排序的角色查询</returns>
    private static IQueryable<Role> ApplyListSort(
        IQueryable<Role> query
        , string? sortField
        , string? sortOrder
    ) {
        return query.ApplyDynamicSort(sortField, sortOrder, nameof(Role.CreatedAt), true, _sortAliases);
    }

    /// <summary>
    ///     构建 BuildTree 方法对应的业务数据
    /// </summary>
    /// <param name="rows">数据行集合</param>
    /// <returns>BuildTree 方法的执行结果</returns>
    private static IReadOnlyList<MenuItemResult> BuildTree(IReadOnlyList<Menu> rows) {
        var nodes = rows.ToDictionary(
            x => x.Name
            , x => new MenuItemResult(
                x.Id, ServerTime.ToOffset(x.CreatedAt), x.UpdatedAt.HasValue ? ServerTime.ToOffset(x.UpdatedAt.Value) : null, x.Name, x.Path
                , x.Component, x.ParentName, x.Sort, x.IsEnabled, ParseMeta(x.MetaJson), []
            ), StringComparer.Ordinal
        );
        return BuildChildren(string.Empty, [with(StringComparer.Ordinal)]);

        IReadOnlyList<MenuItemResult> BuildChildren(
            string parentName
            , HashSet<string> ancestors
        ) {
            return
            [
                .. nodes
                    .Values.Where(x => x.ParentName == parentName && !ancestors.Contains(x.Name))
                    .OrderBy(x => x.Sort)
                    .Select(x =>
                        {
                            var nextAncestors = new HashSet<string>(ancestors, StringComparer.Ordinal) { x.Name };
                            return x with { Children = BuildChildren(x.Name, nextAncestors) };
                        }
                    )
            ];
        }
    }

    /// <summary>
    ///     解析 ParseMeta 方法对应的业务数据
    /// </summary>
    /// <param name="json">JSON 文本</param>
    /// <returns>ParseMeta 方法的执行结果</returns>
    private static JsonElement ParseMeta(string json) {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    /// <summary>
    ///     将角色实体转换为列表项
    /// </summary>
    /// <param name="role">角色实体</param>
    /// <returns>角色列表项</returns>
    private static RoleListItem ToListItem(Role role) {
        return new RoleListItem(
            role.Id, role.Name, role.Code, role.Description, role.DataScope, role.IsEnabled, ServerTime.ToOffset(role.CreatedAt)
            , role.UpdatedAt is { } updatedAt ? ServerTime.ToOffset(updatedAt) : null
        );
    }

    /// <summary>
    ///     构建角色动态筛选查询
    /// </summary>
    /// <param name="dynamicFilter">动态筛选条件</param>
    /// <returns>已应用动态筛选的角色查询</returns>
    private IQueryable<Role> BuildListQuery(DynamicFilter? dynamicFilter) {
        return db.Roles.AsNoTracking().ApplyDynamicFilter(dynamicFilter);
    }

    /// <summary>
    ///     生成不重复的复制角色编码
    /// </summary>
    /// <param name="sourceCode">源角色编码</param>
    /// <returns>新的角色编码</returns>
    private async Task<string> CreateCopyCodeAsync(string sourceCode) {
        var baseCode = $"{sourceCode}_COPY";
        var code = baseCode;
        var codeCopy = code;
        var suffix = 2;
        while (await db.Roles.AnyAsync(x => x.Code == codeCopy).ConfigureAwait(false)) {
            code = $"{baseCode}_{suffix++}";
        }

        return code;
    }
}