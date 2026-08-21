using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

namespace AiAdmin.Api.Data;

/// <summary>
///     数据库结构和基础数据初始化器
/// </summary>
public static class DatabaseInitializer
{
    private static readonly string[] _basicApiKeys =
    [
        ApiEndpointKey.Create("GET", "api/user/info"), ApiEndpointKey.Create("GET", "api/menu/current")
        , ApiEndpointKey.Create("PUT", "api/user/profile"), ApiEndpointKey.Create("PUT", "api/user/password")
        , ApiEndpointKey.Create("GET", "api/user/referrals")
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
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        _ = await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (!await db.Roles.AnyAsync().ConfigureAwait(false)) {
            await db
                .Roles.AddRangeAsync(
                    new Role { Name = "Super administrator", Code = "R_SUPER", Description = "Full system access", DataScope = RoleDataScope.ALL }
                    , new Role
                    {
                        Name = "Administrator"
                        , Code = "R_ADMIN"
                        , Description = "User administration"
                        , DataScope = RoleDataScope.DEPARTMENT_AND_CHILDREN
                    }, new Role { Name = "User", Code = "R_USER", Description = "Basic access", DataScope = RoleDataScope.SELF }
                )
                .ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

        var defaultDepartment = await EnsureDefaultDepartmentAsync(db).ConfigureAwait(false);

        if (!await db.Users.AnyAsync().ConfigureAwait(false)) {
            var superRole = await db.Roles.SingleAsync(x => x.Code == "R_SUPER").ConfigureAwait(false);
            var adminRole = await db.Roles.SingleAsync(x => x.Code == "R_ADMIN").ConfigureAwait(false);
            var userRole = await db.Roles.SingleAsync(x => x.Code == "R_USER").ConfigureAwait(false);

            // 初始化三个基础账号，分别覆盖超级管理员、普通管理员和普通用户权限
            var root = new User
            {
                UserName = "root"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234qwer")
                , Email = "root@aiadmin.local"
                , Phone = "13800000000"
                , Gender = UserGender.Male
            };
            root.UserRoles.Add(new UserRole { User = root, Role = superRole });

            var admin = new User
            {
                UserName = "admin"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234qwer")
                , Email = "admin@aiadmin.local"
                , Phone = "13800000001"
                , Gender = UserGender.Male
            };
            admin.UserRoles.Add(new UserRole { User = admin, Role = adminRole });

            var user = new User
            {
                UserName = "user"
                , PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234qwer")
                , Email = "user@aiadmin.local"
                , Phone = "13800000002"
                , Gender = UserGender.Male
            };
            user.UserRoles.Add(new UserRole { User = user, Role = userRole });

            await db.Users.AddRangeAsync(root, admin, user).ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);

            await AddSeedUserDepartmentsAsync(db, defaultDepartment, root, admin, user).ConfigureAwait(false);
        }

        if (!await db.Menus.AnyAsync().ConfigureAwait(false)) {
            await SeedMenusAsync(db).ConfigureAwait(false);
        }

        await EnsureScheduledJobMenuAsync(db).ConfigureAwait(false);

        if (!await db.RoleMenus.AnyAsync().ConfigureAwait(false)) {
            await SeedRoleMenusAsync(db).ConfigureAwait(false);
        }

        if (!await db.DictionaryCategories.AnyAsync().ConfigureAwait(false)) {
            _ = await db
                .DictionaryCategories.AddAsync(new DictionaryCategory { Code = "system_settings", Name = "System Settings", Sort = 0 })
                .ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

        var systemSettings = await db.DictionaryCategories.SingleAsync(x => x.Code == "system_settings").ConfigureAwait(false);
        var systemSettingSeeds = new[]
        {
            (Label: "Enable login slider verification"
                , Value: configuration.GetValue("SystemSettings:EnableLoginSliderVerification", false) ? "true" : "false", Sort: 0
                , Remark: "Whether login requires slider verification")
            , (Label: "Enable user registration", Value: configuration.GetValue("SystemSettings:EnableUserRegistration", true) ? "true" : "false"
                , Sort: 1, Remark: "Whether public user registration is enabled")
            , (Label: "Enable email verification"
                , Value: configuration.GetValue("SystemSettings:EnableEmailVerification", true) ? "true" : "false", Sort: 2
                , Remark: "Whether registration requires email verification")
            , (Label: "SMTP Host", Value: configuration["SystemSettings:Smtp:Host"] ?? string.Empty, Sort: 3, Remark: "SMTP server host")
            , (Label: "SMTP Port", Value: configuration["SystemSettings:Smtp:Port"] ?? "25", Sort: 4, Remark: "SMTP server port")
            , (Label: "SMTP SSL", Value: configuration.GetValue("SystemSettings:Smtp:EnableSsl", true) ? "true" : "false", Sort: 5
                , Remark: "Whether SMTP SSL is enabled")
            , (Label: "SMTP User", Value: configuration["SystemSettings:Smtp:User"] ?? string.Empty, Sort: 6, Remark: "SMTP login user")
            , (Label: "SMTP Password", Value: configuration["SystemSettings:Smtp:Password"] ?? string.Empty, Sort: 7
                , Remark: "SMTP login password")
            , (Label: "SMTP From", Value: configuration["SystemSettings:Smtp:From"] ?? string.Empty, Sort: 8, Remark: "SMTP sender address")
        };
        var existingSystemSettingLabels = await db
            .DictionaryItems.Where(x => x.CategoryId == systemSettings.Id)
            .Select(x => x.Label)
            .ToHashSetAsync()
            .ConfigureAwait(false);

        // 本地配置仅作为首次建库的种子来源，避免应用重启时覆盖管理后台已经修改的设置
        foreach (var setting in systemSettingSeeds.Where(x => !existingSystemSettingLabels.Contains(x.Label))) {
            _ = await db
                .DictionaryItems.AddAsync(
                    new DictionaryItem
                    {
                        CategoryId = systemSettings.Id
                        , Label = setting.Label
                        , Value = setting.Value
                        , Sort = setting.Sort
                        , IsEnabled = true
                        , Remark = setting.Remark
                    }
                )
                .ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
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

    /// <summary>
    ///     为种子用户创建默认部门下的个人子部门并建立关联
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="defaultDepartment">默认部门</param>
    /// <param name="users">种子用户集合</param>
    /// <returns>异步处理任务</returns>
    private static async Task AddSeedUserDepartmentsAsync(
        AppDbContext db
        , Department defaultDepartment
        , params User[] users
    ) {
        foreach (var user in users) {
            var department = new Department { Name = user.UserName, Code = $"USER_{user.Id}", ParentId = defaultDepartment.Id, Sort = 0 };
            user.UserDepartments.Add(new UserDepartment { User = user, Department = department });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保默认部门种子存在
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>默认部门实体</returns>
    private static async Task<Department> EnsureDefaultDepartmentAsync(AppDbContext db) {
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Code == Department.DEFAULT_CODE).ConfigureAwait(false);
        if (department is not null) {
            if (department.Name != Department.DEFAULT_NAME) {
                department.Name = Department.DEFAULT_NAME;
                _ = await db.SaveChangesAsync().ConfigureAwait(false);
            }

            return department;
        }

        department = new Department { Name = Department.DEFAULT_NAME, Code = Department.DEFAULT_CODE, Sort = 0 };
        _ = await db.Departments.AddAsync(department).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return department;
    }

    /// <summary>
    ///     确保计划作业菜单存在，兼容已有数据库和旧菜单种子
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureScheduledJobMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "ScheduledJobManagement").ConfigureAwait(false)) {
            return;
        }

        if (!await db.Menus.AnyAsync(x => x.Name == "SystemManagement").ConfigureAwait(false)) {
            return;
        }

        _ = await db
            .Menus.AddAsync(
                new Menu
                {
                    Name = "ScheduledJobManagement"
                    , Path = "scheduled-job"
                    , Component = "/system/scheduled-job"
                    , ParentName = "SystemManagement"
                    , Sort = 30
                    , IsEnabled = true
                    , MetaJson = /*lang=json,strict*/
                        "{\"title\":\"menus.system.scheduledJob\",\"icon\":\"ri:timer-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\"]}"
                }
            )
            .ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
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