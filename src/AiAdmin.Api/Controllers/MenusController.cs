using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     菜单管理控制器
/// </summary>
/// <param name="db">应用数据库上下文</param>
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
            x => x.Name
            , x => new MenuItemResult(
                x.Id, ServerTime.ToOffset(x.CreatedAt), x.UpdatedAt.HasValue ? ServerTime.ToOffset(x.UpdatedAt.Value) : null, x.Name, x.Path
                , x.Component, x.ParentName, x.Sort, x.IsEnabled, ParseMeta(x.MetaJson), []
            ), StringComparer.Ordinal
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
    /// <param name="request">菜单标识请求</param>
    /// <returns>删除结果</returns>
    [HttpPost("delete")]
    [ApiDescription("Delete menu")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync([FromBody] IdentifierRequest request) {
        var id = request.Id;
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
    ///     查询菜单列表筛选字段元数据
    /// </summary>
    /// <returns>菜单筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query menu filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<Menu>()));
    }

    /// <summary>
    ///     查询当前菜单筛选条件下的字段分组计数
    /// </summary>
    /// <param name="request">当前动态筛选条件</param>
    /// <returns>菜单筛选分组统计</returns>
    [HttpPost("filter-groups")]
    [ApiDescription("Query menu filter groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListFilterGroupResult>>>> FilterGroupsAsync([FromBody] ListFilterGroupRequest request) {
        var groups = await ListFilterGroupingService.GetGroupsAsync(db.Menus.AsNoTracking(), request.DynamicFilter).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<ListFilterGroupResult>>.Ok(groups));
    }

    /// <summary>
    ///     查询全部菜单树
    /// </summary>
    /// <param name="request">包含动态筛选信息的请求体</param>
    /// <returns>菜单树</returns>
    [HttpPost("list")]
    [ApiDescription("Query menu list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResult>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var sortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meta.title"] = nameof(Menu.Name)
            , ["component"] = nameof(Menu.Component)
            , ["sort"] = nameof(Menu.Sort)
            , ["type"] = nameof(Menu.Name)
            , ["meta.authList"] = nameof(Menu.Name)
            , ["status"] = nameof(Menu.IsEnabled)
            , ["date"] = nameof(Menu.UpdatedAt)
            , ["createdAt"] = nameof(Menu.CreatedAt)
            , ["updatedAt"] = nameof(Menu.UpdatedAt)
        };
        var menus = await db
            .Menus.AsNoTracking()
            .ApplyDynamicFilter(request.DynamicFilter)
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(Menu.CreatedAt), true, sortAliases)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<MenuItemResult>>.Ok(BuildTree(menus)));
    }

    /// <summary>
    ///     更新菜单
    /// </summary>
    /// <param name="request">菜单保存请求</param>
    /// <returns>更新后的菜单</returns>
    [HttpPost("update")]
    [ApiDescription("Update menu")]
    public async Task<ActionResult<ApiResponse<MenuItemResult>>> UpdateAsync([FromBody] SaveMenuRequest request) {
        var id = request.Id.GetValueOrDefault();
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

    /// <summary>
    ///     构建 BuildTree 方法对应的业务数据
    /// </summary>
    /// <param name="rows">数据行集合</param>
    /// <returns>BuildTree 方法的执行结果</returns>
    private static IReadOnlyList<MenuItemResult> BuildTree(IReadOnlyList<Menu> rows) {
        var nodes = rows.ToDictionary(x => x.Name, ToResult, StringComparer.Ordinal);

        return BuildChildren(string.Empty, [with(StringComparer.Ordinal)]);

        IReadOnlyList<MenuItemResult> BuildChildren(
            string parentName
            , HashSet<string> ancestors
        ) {
            return
            [
                .. nodes
                    .Values.Where(x => x.ParentName == parentName && !ancestors.Contains(x.Name))
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
    ///     执行 FromRequest 方法对应的业务逻辑
    /// </summary>
    /// <param name="request">请求参数</param>
    /// <returns>FromRequest 方法的执行结果</returns>
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
    ///     转换 ToResult 方法对应的业务数据
    /// </summary>
    /// <param name="menu">方法参数 menu</param>
    /// <returns>ToResult 方法的执行结果</returns>
    private static MenuItemResult ToResult(Menu menu) {
        return new MenuItemResult(
            menu.Id, ServerTime.ToOffset(menu.CreatedAt), menu.UpdatedAt.HasValue ? ServerTime.ToOffset(menu.UpdatedAt.Value) : null, menu.Name
            , menu.Path, menu.Component, menu.ParentName, menu.Sort, menu.IsEnabled, ParseMeta(menu.MetaJson), []
        );
    }
}