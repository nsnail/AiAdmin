using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

// 提供菜单维护、当前用户菜单树和角色菜单数据所需的接口。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     菜单管理控制器
/// </summary>
[ApiController]
[ApiDescription("Menu management")]
[Authorize]
[Route("api/menu")]
public sealed class MenusController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     创建菜单
    /// </summary>
    /// <param name="request">菜单保存请求</param>
    /// <returns>创建后的菜单</returns>
    [HttpPost]
    [ApiDescription("Create menu")]
    public async Task<ActionResult<ApiResponse<MenuItemResult>>> CreateAsync(SaveMenuRequest request) {
        if (await db.Menus.AnyAsync(x => x.Name == request.Name.Trim()).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Menu name already exists", null));
        }

        var menu = FromRequest(request);
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<MenuItemResult>.Ok(ToResult(menu), "Menu created"));
    }

    /// <summary>
    ///     查询当前用户可访问的菜单树
    /// </summary>
    /// <returns>当前用户菜单树</returns>
    [HttpGet("current")]
    [ApiDescription("Get current user menus")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> CurrentAsync() {
        // 超级管理员读取全部菜单，其他用户读取所有角色菜单的并集。
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var isSuperAdmin = await db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.IsEnabled && x.Role.Code == "R_SUPER").ConfigureAwait(false);
        var rows = isSuperAdmin
            ? await db.Menus.Where(x => x.IsEnabled).AsNoTracking().ToListAsync().ConfigureAwait(false)
            : await db
                .UserRoles.Where(x => x.UserId == userId && x.Role.IsEnabled)
                .SelectMany(x => x.Role.RoleMenus)
                .Select(x => x.Menu)
                .Where(x => x.IsEnabled)
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);
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

    /// <summary>
    ///     删除菜单
    /// </summary>
    /// <param name="id">菜单主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete menu")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var menu = await db.Menus.FindAsync(id).ConfigureAwait(false);
        if (menu is null) {
            return NotFound(new ApiResponse<object>(404, "Menu not found", null));
        }

        if (await db.Menus.AnyAsync(x => x.ParentName == menu.Name).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Delete child menus first", null));
        }

        _ = db.Menus.Remove(menu);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Menu deleted"));
    }

    /// <summary>
    ///     查询全部菜单树
    /// </summary>
    /// <returns>菜单树</returns>
    [HttpGet("list")]
    [ApiDescription("Query menu list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> ListAsync() {
        var menus = await db.Menus.AsNoTracking().OrderBy(x => x.Sort).ToListAsync().ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildTree(menus)));
    }

    /// <summary>
    ///     更新菜单
    /// </summary>
    /// <param name="id">菜单主键</param>
    /// <param name="request">菜单保存请求</param>
    /// <returns>更新后的菜单</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update menu")]
    public async Task<ActionResult<ApiResponse<MenuItemResult>>> UpdateAsync(
        long id
        , SaveMenuRequest request
    ) {
        var menu = await db.Menus.FindAsync(id).ConfigureAwait(false);
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
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
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