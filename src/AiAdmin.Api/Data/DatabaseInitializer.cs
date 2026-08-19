using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

// 负责创建数据库结构并写入开发环境所需的基础数据。
namespace AiAdmin.Api.Data;

/// <summary>
///     数据库结构和基础数据初始化器
/// </summary>
public static class DatabaseInitializer
{
    private static readonly string[] _basicApiKeys =
    [
        ApiEndpointKey.Create("GET", "api/user/info"), ApiEndpointKey.Create("GET", "api/menu/current")
    ];

    private static readonly JsonSerializerOptions _seedJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     初始化数据库结构、基础角色和菜单数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static async Task InitializeAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (!await db.Roles.AnyAsync().ConfigureAwait(false)) {
            await db
                .Roles.AddRangeAsync(
                    new Role { Name = "Super administrator", Code = "R_SUPER", Description = "Full system access" }
                    , new Role { Name = "Administrator", Code = "R_ADMIN", Description = "User administration" }
                    , new Role { Name = "User", Code = "R_USER", Description = "Basic access" }
                )
                .ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

        if (!await db.Users.AnyAsync().ConfigureAwait(false)) {
            var superRole = await db.Roles.SingleAsync(x => x.Code == "R_SUPER").ConfigureAwait(false);
            var adminRole = await db.Roles.SingleAsync(x => x.Code == "R_ADMIN").ConfigureAwait(false);
            var userRole = await db.Roles.SingleAsync(x => x.Code == "R_USER").ConfigureAwait(false);

            // 初始化三个基础账号，分别覆盖超级管理员、普通管理员和普通用户权限
            var root = new User
            {
                UserName = "root"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
                , NickName = "Super administrator"
                , Email = "root@aiadmin.local"
                , Phone = "13800000000"
                , Gender = "male"
            };
            root.UserRoles.Add(new UserRole { User = root, Role = superRole });

            var admin = new User
            {
                UserName = "admin"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
                , NickName = "Administrator"
                , Email = "admin@aiadmin.local"
                , Phone = "13800000001"
                , Gender = "male"
            };
            admin.UserRoles.Add(new UserRole { User = admin, Role = adminRole });

            var user = new User
            {
                UserName = "user"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
                , NickName = "User"
                , Email = "user@aiadmin.local"
                , Phone = "13800000002"
                , Gender = "male"
            };
            user.UserRoles.Add(new UserRole { User = user, Role = userRole });

            await db.Users.AddRangeAsync(root, admin, user).ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

        if (!await db.Menus.AnyAsync().ConfigureAwait(false)) {
            await SeedMenusAsync(db).ConfigureAwait(false);
        }

        if (!await db.RoleMenus.AnyAsync().ConfigureAwait(false)) {
            await SeedRoleMenusAsync(db).ConfigureAwait(false);
        }

        if (!await db.DictionaryCategories.AnyAsync().ConfigureAwait(false)) {
            _ = await db
                .DictionaryCategories.AddAsync(new DictionaryCategory { Code = "system_settings", Name = "系统设置", Sort = 0 })
                .ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     为非超级管理员角色补充进入后台所需的基础接口权限
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static async Task InitializeRoleApisAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionCache = scope.ServiceProvider.GetRequiredService<ApiPermissionCache>();
        var roles = await db.Roles.Include(x => x.RoleApis).Where(x => x.Code == "R_ADMIN" || x.Code == "R_USER").ToListAsync().ConfigureAwait(false);
        var endpoints = (await db.ApiEndpoints.ToListAsync().ConfigureAwait(false))
            .Where(x => _basicApiKeys.Contains(ApiEndpointKey.Create(x.Method, x.Path), StringComparer.Ordinal))
            .ToList();

        // 接口同步完成后，幂等补齐普通管理员和普通用户登录后台必需的权限
        foreach (var role in roles) {
            var assignedEndpointIds = role.RoleApis.Select(x => x.ApiEndpointId).ToHashSet();
            foreach (var endpoint in endpoints.Where(x => !assignedEndpointIds.Contains(x.Id))) {
                role.RoleApis.Add(new RoleApi { Role = role, ApiEndpoint = endpoint });
            }
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        permissionCache.Invalidate();
    }

    private static MenuItemRequest[] FilterByRole(
        IEnumerable<MenuItemRequest> menus
        , string roleCode
    ) {
        return
        [
            .. menus
                .Where(menu => HasRole(menu.Meta, roleCode))
                .Select(menu => new MenuItemRequest
                    {
                        Name = menu.Name
                        , Path = menu.Path
                        , Component = menu.Component
                        , Meta = menu.Meta
                        , Children = FilterByRole(menu.Children, roleCode)
                    }
                )
        ];
    }

    private static IEnumerable<(MenuItemRequest Menu, string ParentName, int Sort)> Flatten(
        IEnumerable<MenuItemRequest> menus
        , string parentName = ""
    ) {
        var index = 0;
        foreach (var menu in menus) {
            yield return (menu, parentName, index);
            ++index;
            foreach (var child in Flatten(menu.Children, menu.Name)) {
                yield return child;
            }
        }
    }

    private static IEnumerable<(MenuItemRequest Menu, string ParentName)> FlattenSeed(
        IEnumerable<MenuItemRequest> menus
        , string parent = ""
    ) {
        foreach (var menu in menus) {
            yield return (menu, parent);
            foreach (var child in FlattenSeed(menu.Children, menu.Name)) {
                yield return child;
            }
        }
    }

    private static bool HasRole(
        JsonElement meta
        , string roleCode
    ) {
        return meta.ValueKind != JsonValueKind.Object
               || !meta.TryGetProperty("roles", out var roles)
               || roles.ValueKind != JsonValueKind.Array
               || roles.EnumerateArray().Any(x => x.GetString() == roleCode);
    }

    private static async Task SeedMenusAsync(AppDbContext db) {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "menu-seed.json");
        var json = await File.ReadAllTextAsync(seedPath).ConfigureAwait(false);
        var tree = JsonSerializer.Deserialize<MenuItemRequest[]>(json, _seedJsonOptions) ?? [];
        var index = 0;
        foreach (var item in FlattenSeed(tree)) {
            var sort = index;
            ++index;
            _ = await db
                .Menus.AddAsync(
                    new Menu
                    {
                        Name = item.Menu.Name
                        , Path = item.Menu.Path
                        , Component = item.Menu.Component
                        , ParentName = item.ParentName
                        , Sort = sort
                        , MetaJson = item.Menu.Meta.GetRawText()
                    }
                )
                .ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedRoleMenusAsync(AppDbContext db) {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "menu-seed.json");
        var json = await File.ReadAllTextAsync(seedPath).ConfigureAwait(false);
        var menuTree = JsonSerializer.Deserialize<MenuItemRequest[]>(json, _seedJsonOptions) ?? [];
        var roles = await db.Roles.ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            if (role.Code == "R_SUPER") {
                continue;
            }

            foreach (var item in Flatten(FilterByRole(menuTree, role.Code)).ToList()) {
                var menu = await db.Menus.SingleAsync(x => x.Name == item.Menu.Name).ConfigureAwait(false);
                role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
            }
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }
}