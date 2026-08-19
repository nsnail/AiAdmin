using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Roles.AnyAsync()) {
            db.Roles.AddRange(
                new Role { Name = "Super administrator", Code = "R_SUPER", Description = "Full system access" }
                , new Role { Name = "Administrator", Code = "R_ADMIN", Description = "User administration" }
                , new Role { Name = "User", Code = "R_USER", Description = "Basic access" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync()) {
            var superRole = await db.Roles.SingleAsync(x => x.Code == "R_SUPER");
            var admin = new User
            {
                UserName = "admin"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
                , NickName = "Administrator"
                , Email = "admin@aiadmin.local"
                , Phone = "13800000000"
                , Gender = "male"
            };
            admin.UserRoles.Add(new UserRole { User = admin, Role = superRole });
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync()) {
            await SeedMenusAsync(db);
        }

        if (!await db.RoleMenus.AnyAsync()) {
            await SeedRoleMenusAsync(db);
        }
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
            yield return (menu, parentName, index++);
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
        if (meta.ValueKind != JsonValueKind.Object || !meta.TryGetProperty("roles", out var roles)) {
            return true;
        }

        return roles.ValueKind != JsonValueKind.Array || roles.EnumerateArray().Any(x => x.GetString() == roleCode);
    }

    private static async Task SeedMenusAsync(AppDbContext db) {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "menu-seed.json");
        var json = await File.ReadAllTextAsync(seedPath);
        var tree = JsonSerializer.Deserialize<MenuItemRequest[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var index = 0;
        foreach (var item in FlattenSeed(tree)) {
            db.Menus.Add(
                new Menu
                {
                    Name = item.Menu.Name
                    , Path = item.Menu.Path
                    , Component = item.Menu.Component
                    , ParentName = item.ParentName
                    , Sort = index++
                    , MetaJson = item.Menu.Meta.GetRawText()
                }
            );
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedRoleMenusAsync(AppDbContext db) {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "menu-seed.json");
        var json = await File.ReadAllTextAsync(seedPath);
        var menuTree = JsonSerializer.Deserialize<MenuItemRequest[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var roles = await db.Roles.ToListAsync();
        foreach (var role in roles) {
            var items = Flatten(FilterByRole(menuTree, role.Code)).ToList();
            foreach (var item in items) {
                var menu = await db.Menus.SingleAsync(x => x.Name == item.Menu.Name);
                role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
            }
        }

        await db.SaveChangesAsync();
    }
}