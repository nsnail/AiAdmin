using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/menu")]
public sealed class MenusController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "R_SUPER")]
    public async Task<ActionResult<ApiResponse<MenuItemResult>>> Create(SaveMenuRequest request) {
        if (await db.Menus.AnyAsync(x => x.Name == request.Name.Trim())) {
            return Conflict(new ApiResponse<object>(409, "Menu name already exists", null));
        }

        var menu = FromRequest(request);
        db.Menus.Add(menu);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<MenuItemResult>.Ok(ToResult(menu), "Menu created"));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> Current() {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var rows = await db
            .UserRoles.Where(x => x.UserId == userId && x.Role.IsEnabled)
            .SelectMany(x => x.Role.RoleMenus)
            .Select(x => x.Menu)
            .Where(x => x.IsEnabled)
            .AsNoTracking()
            .ToListAsync();
        var unique = rows.GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.Sort).ToList();
        var nodes = unique.ToDictionary(
            x => x.Name, x => new MenuItemResult(x.Id, x.Name, x.Path, x.Component, x.ParentName, x.Sort, ParseMeta(x.MetaJson), [])
            , StringComparer.Ordinal
        );

        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildChildren(string.Empty)));

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

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "R_SUPER")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) {
        var menu = await db.Menus.FindAsync(id);
        if (menu is null) {
            return NotFound(new ApiResponse<object>(404, "Menu not found", null));
        }

        if (await db.Menus.AnyAsync(x => x.ParentName == menu.Name)) {
            return BadRequest(new ApiResponse<object>(400, "Delete child menus first", null));
        }

        db.Menus.Remove(menu);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }, "Menu deleted"));
    }

    [HttpGet("list")]
    [Authorize(Roles = "R_SUPER")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> List() {
        var menus = await db.Menus.AsNoTracking().OrderBy(x => x.Sort).ToListAsync();
        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildTree(menus)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "R_SUPER")]
    public async Task<ActionResult<ApiResponse<MenuItemResult>>> Update(
        long id
        , SaveMenuRequest request
    ) {
        var menu = await db.Menus.FindAsync(id);
        if (menu is null) {
            return NotFound(new ApiResponse<object>(404, "Menu not found", null));
        }

        menu.Name = request.Name.Trim();
        menu.Path = request.Path.Trim();
        menu.Component = request.Component.Trim();
        menu.ParentName = request.ParentName.Trim();
        menu.Sort = request.Sort;
        menu.IsEnabled = request.IsEnabled;
        menu.MetaJson = request.Meta.ValueKind == JsonValueKind.Undefined ? "{}" : request.Meta.GetRawText();
        await db.SaveChangesAsync();
        return Ok(ApiResponse<MenuItemResult>.Ok(ToResult(menu), "Menu updated"));
    }

    private static IReadOnlyList<MenuItemResult> BuildTree(IReadOnlyList<Menu> rows) {
        var nodes = rows.ToDictionary(x => x.Name, ToResult, StringComparer.Ordinal);

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

    private static Menu FromRequest(SaveMenuRequest request) {
        return new Menu
        {
            Name = request.Name.Trim()
            , Path = request.Path.Trim()
            , Component = request.Component.Trim()
            , ParentName = request.ParentName.Trim()
            , Sort = request.Sort
            , IsEnabled = request.IsEnabled
            , MetaJson = request.Meta.ValueKind == JsonValueKind.Undefined ? "{}" : request.Meta.GetRawText()
        };
    }

    private static JsonElement ParseMeta(string json) {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static MenuItemResult ToResult(Menu menu) {
        return new MenuItemResult(menu.Id, menu.Name, menu.Path, menu.Component, menu.ParentName, menu.Sort, ParseMeta(menu.MetaJson), []);
    }
}