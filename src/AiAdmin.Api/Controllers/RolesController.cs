using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

[ApiController]
[Authorize(Roles = "R_SUPER")]
[Route("api/role")]
public sealed class RolesController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> Create(SaveRoleRequest request) {
        var code = request.RoleCode.Trim();
        if (await db.Roles.AnyAsync(x => x.Code == code)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        var role = new Role { Name = request.RoleName.Trim(), Code = code, Description = request.Description.Trim(), IsEnabled = request.Enabled };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(role), "Role created"));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) {
        var role = await db.Roles.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        if (role.UserRoles.Count > 0) {
            return BadRequest(new ApiResponse<object>(400, "Role is assigned to users", null));
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }, "Role deleted"));
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<RoleListItem>>>> List(
        [FromQuery] int current = 1
        , [FromQuery] int size = 20
        , [FromQuery] string? roleName = null
        , [FromQuery] string? roleCode = null
        , [FromQuery] string? description = null
        , [FromQuery] bool? enabled = null
    ) {
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 100);
        var query = db.Roles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(roleName)) {
            query = query.Where(x => x.Name.Contains(roleName));
        }

        if (!string.IsNullOrWhiteSpace(roleCode)) {
            query = query.Where(x => x.Code.Contains(roleCode));
        }

        if (!string.IsNullOrWhiteSpace(description)) {
            query = query.Where(x => x.Description.Contains(description));
        }

        if (enabled.HasValue) {
            query = query.Where(x => x.IsEnabled == enabled.Value);
        }

        var total = await query.CountAsync();
        var roles = await query.OrderBy(x => x.Id).Skip((current - 1) * size).Take(size).ToListAsync();
        var items = roles.Select(ToListItem).ToList();
        return Ok(ApiResponse<PagedResponse<RoleListItem>>.Ok(new PagedResponse<RoleListItem>(items, current, size, total)));
    }

    [HttpGet("{id:long}/menus")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> Menus(long id) {
        if (!await db.Roles.AnyAsync(x => x.Id == id)) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var rows = await db
            .RoleMenus.AsNoTracking()
            .Where(x => x.RoleId == id)
            .Include(x => x.Menu)
            .Select(x => x.Menu)
            .OrderBy(x => x.Sort)
            .ToListAsync();
        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildTree(rows)));
    }

    [HttpPut("{id:long}/menus")]
    public async Task<ActionResult<ApiResponse<object>>> SaveMenus(
        long id
        , SaveRoleMenusRequest request
    ) {
        var role = await db.Roles.Include(x => x.RoleMenus).SingleOrDefaultAsync(x => x.Id == id);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        db.RoleMenus.RemoveRange(role.RoleMenus);
        var menus = await db.Menus.Where(x => request.MenuIds.Distinct().Contains(x.Id)).ToListAsync();
        role.RoleMenus = [.. menus.Select(menu => new RoleMenu { Role = role, Menu = menu })];
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }, "Menu permissions saved"));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<RoleListItem>>> Update(
        long id
        , SaveRoleRequest request
    ) {
        var role = await db.Roles.FindAsync(id);
        if (role is null) {
            return NotFound(new ApiResponse<object>(404, "Role not found", null));
        }

        var code = request.RoleCode.Trim();
        if (await db.Roles.AnyAsync(x => x.Code == code && x.Id != id)) {
            return Conflict(new ApiResponse<object>(409, "Role code already exists", null));
        }

        role.Name = request.RoleName.Trim();
        role.Code = code;
        role.Description = request.Description.Trim();
        role.IsEnabled = request.Enabled;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<RoleListItem>.Ok(ToListItem(role), "Role updated"));
    }

    private static IReadOnlyList<MenuItemResult> BuildTree(IReadOnlyList<Menu> rows) {
        var nodes = rows.ToDictionary(
            x => x.Name, x => new MenuItemResult(x.Id, x.Name, x.Path, x.Component, x.ParentName, x.Sort, ParseMeta(x.MetaJson), [])
            , StringComparer.Ordinal
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
        return new RoleListItem(role.Id, role.Name, role.Code, role.Description, role.IsEnabled, DateTime.UnixEpoch);
    }
}