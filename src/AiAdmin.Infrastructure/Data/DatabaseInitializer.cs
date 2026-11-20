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
    /// <summary>
    ///     所有非超级管理员角色登录后台必需的接口：个人信息、当前菜单、个人资料、站内信通知、钱包、更新日志、仪表盘，
    ///     以及每个用户自己的列表查询条件。启用状态切换、消息管理和用户管理等后台接口不在此列，需在角色管理中单独授权
    /// </summary>
    public static readonly IReadOnlyList<string> BasicApiKeys =
    [
        ApiEndpointKey.Create("GET", "api/user/info"), ApiEndpointKey.Create("GET", "api/menu/current")
        , ApiEndpointKey.Create("POST", "api/user/profile"), ApiEndpointKey.Create("POST", "api/user/password")
        , ApiEndpointKey.Create("POST", "api/user/profile/avatar"), ApiEndpointKey.Create("POST", "api/user/profile/avatar/delete")
        , ApiEndpointKey.Create("POST", "api/auth/profile-email/code"), ApiEndpointKey.Create("GET", "api/user/referrals")
        , ApiEndpointKey.Create("GET", "api/notifications"), ApiEndpointKey.Create("POST", "api/notifications/read")
        , ApiEndpointKey.Create("POST", "api/notifications/read-all"), ApiEndpointKey.Create("POST", "api/notifications/delete")
        , ApiEndpointKey.Create("POST", "api/notifications/clear"), ApiEndpointKey.Create("GET", "api/wallet/me")
        , ApiEndpointKey.Create("GET", "api/change-log/content"), ApiEndpointKey.Create("GET", "api/saved-query")
        , ApiEndpointKey.Create("POST", "api/saved-query"), ApiEndpointKey.Create("POST", "api/saved-query/delete")
        , ApiEndpointKey.Create("POST", "api/support/send"), ApiEndpointKey.Create("GET", "api/support/my/messages")
        , ApiEndpointKey.Create("GET", "api/support/my/state"), ApiEndpointKey.Create("GET", "api/user/api-key")
        , ApiEndpointKey.Create("POST", "api/user/api-key/reset"), ApiEndpointKey.Create("POST", "api/user/api-key/delete")
    ];

    /// <summary>
    ///     系统总览页使用的接口，授予除普通用户（R_USER）外的非超级管理员角色；普通用户没有系统总览菜单，启动时还会撤销其已有的这些授权
    /// </summary>
    public static readonly IReadOnlyList<string> DashboardApiKeys =
    [
        ApiEndpointKey.Create("GET", "api/dashboard/overview"), ApiEndpointKey.Create("GET", "api/dashboard/system-info")
    ];

    /// <summary>
    ///     在线客服工作台接口，仅授予管理员角色，普通用户只能使用自己的会话接口
    /// </summary>
    public static readonly IReadOnlyList<string> SupportServiceApiKeys =
    [
        ApiEndpointKey.Create("POST", "api/support/list"), ApiEndpointKey.Create("GET", "api/support/filter-fields")
        , ApiEndpointKey.Create("GET", "api/support/messages"), ApiEndpointKey.Create("POST", "api/support/reply")
        , ApiEndpointKey.Create("POST", "api/support/close")
    ];

    private static readonly JsonSerializerOptions _seedJsonOptions = JsonParsing.Options;

    /// <summary>
    ///     删除当前应用数据库，供开发和测试环境启动时重置数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步删除任务</returns>
    public static async Task DeleteAllTablesAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     初始化系统数据和业务数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static async Task InitializeAsync(IServiceProvider services) {
        await InitializeSystemDataAsync(services).ConfigureAwait(false);
        await InitializeBusinessDataAsync(services).ConfigureAwait(false);
    }

    /// <summary>
    ///     初始化业务数据种子
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static Task InitializeBusinessDataAsync(IServiceProvider services) {
        return BusinessDataSeeder.InitializeAsync(services);
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
        var roles = await db.Roles.Include(x => x.RoleApis).Where(x => x.Code != "R_SUPER").ToListAsync().ConfigureAwait(false);
        var allEndpoints = await db.ApiEndpoints.ToListAsync().ConfigureAwait(false);
        var endpoints = SelectEndpoints(allEndpoints, BasicApiKeys);
        var dashboardEndpoints = SelectEndpoints(allEndpoints, DashboardApiKeys);
        var serviceEndpoints = SelectEndpoints(allEndpoints, SupportServiceApiKeys);

        // 接口同步完成后，幂等补齐各角色登录后台必需的权限，客服工作台接口只授予管理员，系统总览接口不授予普通用户
        foreach (var role in roles) {
            GrantMissingEndpoints(role, ResolveGrantedEndpoints(role.Code, endpoints, dashboardEndpoints, serviceEndpoints));
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await RevokeUserEndpointsAsync(db, dashboardEndpoints).ConfigureAwait(false);
        permissionCache.Invalidate();
    }

    /// <summary>
    ///     初始化数据库结构和系统数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static Task InitializeSystemDataAsync(IServiceProvider services) {
        return SystemDataSeeder.InitializeAsync(services);
    }

    internal static async Task InitializeSystemCoreAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        _ = await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (!await db.Roles.AnyAsync().ConfigureAwait(false)) {
            await db
                .Roles.AddRangeAsync(
                    new Role { Name = "Super administrator", Code = "R_SUPER", Description = "Full system access", DataScope = RoleDataScope.All }
                    , new Role
                    {
                        Name = "Administrator"
                        , Code = "R_ADMIN"
                        , Description = "User administration"
                        , DataScope = RoleDataScope.DepartmentAndChildren
                    }, new Role { Name = "User", Code = "R_USER", Description = "Basic access", DataScope = RoleDataScope.Self }
                )
                .ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

        if (!await db.Menus.AnyAsync().ConfigureAwait(false)) {
            await SeedMenusAsync(db).ConfigureAwait(false);
        }

        await EnsureScheduledJobMenuAsync(db).ConfigureAwait(false);
        await EnsureRedisCacheMenuAsync(db).ConfigureAwait(false);
        await EnsureAlertMonitorMenuAsync(db).ConfigureAwait(false);
        await EnsureRateLimitMenuAsync(db).ConfigureAwait(false);
        await EnsureCircuitBreakerMenuAsync(db).ConfigureAwait(false);
        await EnsureLoginLogMenuAsync(db).ConfigureAwait(false);
        await EnsureApiRequestLogMenuAsync(db).ConfigureAwait(false);
        await EnsureMessageMenuAsync(db).ConfigureAwait(false);
        await EnsureSupportChatMenuAsync(db).ConfigureAwait(false);

        if (!await db.RoleMenus.AnyAsync().ConfigureAwait(false)) {
            await SeedRoleMenusAsync(db).ConfigureAwait(false);
        }

        // 必须放在角色菜单种子之后：它们会写入 RoleMenus，放在前面会让上面的种子授权被误判为已初始化而跳过
        await EnsureUserCenterMenuAsync(db).ConfigureAwait(false);
        await EnsureQuickTilesMenuAsync(db).ConfigureAwait(false);
        await RevokeUserConsoleMenuAsync(db).ConfigureAwait(false);

        var systemSettings = await db.DictionaryCategories.SingleOrDefaultAsync(x => x.Code == "system_settings").ConfigureAwait(false);
        if (systemSettings is null) {
            systemSettings = new DictionaryCategory { Code = "system_settings", Name = "System Settings", Sort = 0 };
            _ = await db.DictionaryCategories.AddAsync(systemSettings).ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
        }

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
            , (Label: "Maximum export rows", Value: configuration["SystemSettings:MaximumExportRows"] ?? "10000", Sort: 9
                , Remark: "Maximum number of rows allowed per export")
            , (Label: "Telegram Bot Token", Value: configuration["Telegram:BotToken"] ?? string.Empty, Sort: 10
                , Remark: "Telegram query bot token")
            , (Label: "Telegram Alert Bot Token", Value: configuration["Telegram:AlertBotToken"] ?? string.Empty, Sort: 11
                , Remark: "Telegram alert bot token")
            , (Label: "Telegram Alert Chat Id", Value: configuration["Telegram:AlertChatId"] ?? string.Empty, Sort: 12
                , Remark: "Telegram chat id that receives scheduled job alerts")
            , (Label: "Enable request signature", Value: configuration.GetValue("SystemSettings:EnableRequestSignature", false) ? "true" : "false"
                , Sort: 13, Remark: "Whether authenticated requests must carry a valid HMAC signature, timestamp and one-time nonce")
        };
        var existingSystemSettingLabels = await db
            .DictionaryItems.Where(x => x.CategoryId == systemSettings.Id)
            .Select(x => x.Label)
            .ToHashSetAsync()
            .ConfigureAwait(false);

        // 本地配置仅作为首次建库的种子来源，避免应用重启时覆盖管理后台已经修改的设置
        foreach (var (label, value, sort, remark) in systemSettingSeeds.Where(x => !existingSystemSettingLabels.Contains(x.Label))) {
            _ = await db
                .DictionaryItems.AddAsync(
                    new DictionaryItem
                    {
                        CategoryId = systemSettings.Id
                        , Label = label
                        , Value = value
                        , Sort = sort
                        , IsEnabled = true
                        , Remark = remark
                    }
                )
                .ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保告警监控菜单存在并授权给超级管理员，兼容已有数据库和旧菜单种子；挂在运维管理下，运维管理不存在时挂在系统管理下
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureAlertMonitorMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "AlertMonitorManagement").ConfigureAwait(false)) {
            return;
        }

        // 告警监控属于运维范畴，优先挂在运维管理下；运维管理是部署时手工创建的菜单，不存在时回退到系统管理
        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "OpsManagement").ConfigureAwait(false)
                     ?? await db.Menus.SingleOrDefaultAsync(x => x.Name == "SystemManagement").ConfigureAwait(false);
        if (parent is null) {
            return;
        }

        var menu = new Menu
        {
            Name = "AlertMonitorManagement"
            , Path = "alert-monitor"
            , Component = "/system/alert-monitor"
            , ParentName = parent.Name
            , Sort = 50
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.system.alertMonitor\",\"icon\":\"ri:alarm-warning-line\",\"keepAlive\":true,\"authList\":[{\"title\":\"Delete\",\"authMark\":\"delete\"},{\"title\":\"Enable\",\"authMark\":\"enable\"},{\"title\":\"Export\",\"authMark\":\"export\"}],\"roles\":[\"R_SUPER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保请求日志菜单存在，兼容已有数据库和旧菜单种子，菜单仅超级管理员可见
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureApiRequestLogMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "ApiRequestLog").ConfigureAwait(false)) {
            return;
        }

        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "LogManagement").ConfigureAwait(false);
        if (parent is null) {
            return;
        }

        var menu = new Menu
        {
            Name = "ApiRequestLog"
            , Path = "api-request-log"
            , Component = "/system/api-request-log"
            , ParentName = parent.Name
            , Sort = 2
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.log.apiRequestLog\",\"icon\":\"ri:route-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保接口熔断菜单存在并授权给超级管理员，兼容已有数据库和旧菜单种子；挂在运维管理下，运维管理不存在时挂在系统管理下
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureCircuitBreakerMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "CircuitBreakerManagement").ConfigureAwait(false)) {
            return;
        }

        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "OpsManagement").ConfigureAwait(false)
                     ?? await db.Menus.SingleOrDefaultAsync(x => x.Name == "SystemManagement").ConfigureAwait(false);
        if (parent is null) {
            return;
        }

        var menu = new Menu
        {
            Name = "CircuitBreakerManagement"
            , Path = "circuit-breaker"
            , Component = "/system/circuit-breaker"
            , ParentName = parent.Name
            , Sort = 46
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.system.circuitBreaker\",\"icon\":\"ri:flashlight-line\",\"keepAlive\":true,\"authList\":[{\"title\":\"Delete\",\"authMark\":\"delete\"},{\"title\":\"Enable\",\"authMark\":\"enable\"},{\"title\":\"Export\",\"authMark\":\"export\"}],\"roles\":[\"R_SUPER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保登录日志菜单存在，兼容已有数据库和旧菜单种子
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureLoginLogMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "LoginLog").ConfigureAwait(false)) {
            return;
        }

        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "LogManagement").ConfigureAwait(false);
        if (parent is null) {
            return;
        }

        var menu = new Menu
        {
            Name = "LoginLog"
            , Path = "login-log"
            , Component = "/system/login-log"
            , ParentName = parent.Name
            , Sort = 1
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.log.loginLog\",\"icon\":\"ri:login-box-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保已有数据库包含消息管理菜单
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureMessageMenuAsync(AppDbContext db) {
        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "SystemManagement").ConfigureAwait(false);
        if (parent is null || await db.Menus.AnyAsync(x => x.Name == "MessageManagement").ConfigureAwait(false)) {
            return;
        }

        if (!parent.MetaJson.Contains("R_ADMIN", StringComparison.Ordinal)) {
            parent.MetaJson = parent.MetaJson.Replace("\"R_SUPER\"", "\"R_SUPER\",\"R_ADMIN\"", StringComparison.Ordinal);
        }

        var menu = new Menu
        {
            Name = "MessageManagement"
            , Path = "message"
            , Component = "/system/message"
            , ParentName = parent.Name
            , Sort = 5
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.system.message\",\"icon\":\"ri:mail-send-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\",\"R_ADMIN\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER" || x.Code == "R_ADMIN").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保已有数据库包含常用功能菜单并授权给普通用户，兼容旧菜单种子；仅在菜单首次创建时授权，之后角色授权由角色管理维护
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureQuickTilesMenuAsync(AppDbContext db) {
        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "Dashboard").ConfigureAwait(false);
        if (parent is null || await db.Menus.AnyAsync(x => x.Name == "QuickTiles").ConfigureAwait(false)) {
            return;
        }

        var menu = new Menu
        {
            Name = "QuickTiles"
            , Path = "tiles"
            , Component = "/dashboard/tiles"
            , ParentName = parent.Name
            , Sort = await db.Menus.Where(x => x.ParentName == parent.Name).Select(x => (int?)x.Sort).MaxAsync().ConfigureAwait(false) + 1 ?? 0
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.dashboard.tiles\",\"icon\":\"ri:apps-2-line\",\"keepAlive\":true,\"roles\":[\"R_USER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Include(x => x.RoleMenus).Where(x => x.Code == "R_USER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            // 子菜单必须连同所在目录一起授权，否则菜单树中找不到父级，页面不会出现
            if (role.RoleMenus.All(x => x.MenuId != parent.Id)) {
                role.RoleMenus.Add(new RoleMenu { Role = role, Menu = parent });
            }

            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保接口限流菜单存在并授权给超级管理员，兼容已有数据库和旧菜单种子；挂在运维管理下，运维管理不存在时挂在系统管理下
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureRateLimitMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "RateLimitManagement").ConfigureAwait(false)) {
            return;
        }

        // 限流属于运维范畴，优先挂在运维管理下；运维管理是部署时手工创建的菜单，不存在时回退到系统管理
        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "OpsManagement").ConfigureAwait(false)
                     ?? await db.Menus.SingleOrDefaultAsync(x => x.Name == "SystemManagement").ConfigureAwait(false);
        if (parent is null) {
            return;
        }

        var menu = new Menu
        {
            Name = "RateLimitManagement"
            , Path = "rate-limit"
            , Component = "/system/rate-limit"
            , ParentName = parent.Name
            , Sort = 45
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.system.rateLimit\",\"icon\":\"ri:speed-up-line\",\"keepAlive\":true,\"authList\":[{\"title\":\"Delete\",\"authMark\":\"delete\"},{\"title\":\"Enable\",\"authMark\":\"enable\"},{\"title\":\"Export\",\"authMark\":\"export\"}],\"roles\":[\"R_SUPER\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保已有数据库包含 Redis 缓存管理菜单
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureRedisCacheMenuAsync(AppDbContext db) {
        if (await db.Menus.AnyAsync(x => x.Name == "RedisCacheManagement").ConfigureAwait(false)) {
            return;
        }

        if (!await db.Menus.AnyAsync(x => x.Name == "SystemManagement").ConfigureAwait(false)) {
            return;
        }

        _ = await db
            .Menus.AddAsync(
                new Menu
                {
                    Name = "RedisCacheManagement"
                    , Path = "cache"
                    , Component = "/system/cache"
                    , ParentName = "SystemManagement"
                    , Sort = 40
                    , IsEnabled = true
                    , MetaJson = /*lang=json,strict*/
                        "{\"title\":\"menus.system.cache\",\"icon\":\"ri:database-2-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\"]}"
                }
            )
            .ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
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
                        "{\"title\":\"menus.system.scheduledJob\",\"icon\":\"ri:timer-line\",\"keepAlive\":true,\"authList\":[{\"title\":\"Enable\",\"authMark\":\"enable\"},{\"title\":\"Export\",\"authMark\":\"export\"}],\"roles\":[\"R_SUPER\"]}"
                }
            )
            .ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保已有数据库包含在线客服菜单，仅授权超级管理员和管理员角色
    /// </summary>
    /// <param name="db">应用数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureSupportChatMenuAsync(AppDbContext db) {
        var parent = await db.Menus.SingleOrDefaultAsync(x => x.Name == "SystemManagement").ConfigureAwait(false);
        if (parent is null || await db.Menus.AnyAsync(x => x.Name == "SupportChat").ConfigureAwait(false)) {
            return;
        }

        var menu = new Menu
        {
            Name = "SupportChat"
            , Path = "support-chat"
            , Component = "/system/support-chat"
            , ParentName = parent.Name
            , Sort = 6
            , IsEnabled = true
            , MetaJson = /*lang=json,strict*/
                "{\"title\":\"menus.system.supportChat\",\"icon\":\"ri:customer-service-2-line\",\"keepAlive\":true,\"roles\":[\"R_SUPER\",\"R_ADMIN\"]}"
        };
        _ = await db.Menus.AddAsync(menu).ConfigureAwait(false);
        var roles = await db.Roles.Where(x => x.Code == "R_SUPER" || x.Code == "R_ADMIN").ToListAsync().ConfigureAwait(false);
        foreach (var role in roles) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     确保普通用户拥有个人中心菜单及其所在的系统管理目录，兼容已有数据库和旧菜单种子；个人中心是隐藏菜单，仅用于注册路由
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步初始化任务</returns>
    private static async Task EnsureUserCenterMenuAsync(AppDbContext db) {
        var menus = await db.Menus.Where(x => x.Name == "SystemManagement" || x.Name == "UserCenter").ToListAsync().ConfigureAwait(false);
        var role = await db.Roles.Include(x => x.RoleMenus).SingleOrDefaultAsync(x => x.Code == "R_USER").ConfigureAwait(false);
        if (role is null) {
            return;
        }

        var assignedMenuIds = role.RoleMenus.Select(x => x.MenuId).ToHashSet();
        var missing = menus.Where(x => !assignedMenuIds.Contains(x.Id)).ToList();
        if (missing.Count == 0) {
            return;
        }

        foreach (var menu in missing) {
            role.RoleMenus.Add(new RoleMenu { Role = role, Menu = menu });
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     筛选 FilterByRole 方法对应的业务数据
    /// </summary>
    /// <param name="menus">方法参数 menus</param>
    /// <param name="roleCode">方法参数 roleCode</param>
    /// <returns>FilterByRole 方法的执行结果</returns>
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

    /// <summary>
    ///     展平 Flatten 方法对应的业务数据
    /// </summary>
    /// <param name="menus">待展平的菜单集合</param>
    /// <param name="parentName">父级菜单名称</param>
    /// <returns>Flatten 方法的执行结果</returns>
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

    /// <summary>
    ///     展平 FlattenSeed 方法对应的业务数据
    /// </summary>
    /// <param name="menus">待展平的种子菜单集合</param>
    /// <param name="parent">父级菜单名称</param>
    /// <returns>FlattenSeed 方法的执行结果</returns>
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

    /// <summary>
    ///     为角色补齐尚未拥有的接口授权
    /// </summary>
    /// <param name="role">已加载接口授权的角色</param>
    /// <param name="granted">应拥有的接口</param>
    private static void GrantMissingEndpoints(
        Role role
        , IEnumerable<ApiEndpoint> granted
    ) {
        var assignedEndpointIds = role.RoleApis.Select(x => x.ApiEndpointId).ToHashSet();
        foreach (var endpoint in granted.Where(x => !assignedEndpointIds.Contains(x.Id))) {
            role.RoleApis.Add(new RoleApi { Role = role, ApiEndpoint = endpoint });
        }
    }

    /// <summary>
    ///     判断是否包含 HasRole 方法对应的业务数据
    /// </summary>
    /// <param name="meta">方法参数 meta</param>
    /// <param name="roleCode">方法参数 roleCode</param>
    /// <returns>HasRole 方法的执行结果</returns>
    private static bool HasRole(
        JsonElement meta
        , string roleCode
    ) {
        return meta.ValueKind == JsonValueKind.Object
               && meta.TryGetPropertyIgnoreCase("roles", out var roles)
               && roles.ValueKind == JsonValueKind.Array
               && roles.EnumerateArray().Any(x => x.GetString() == roleCode);
    }

    /// <summary>
    ///     按角色决定默认授予的接口：普通用户只有基础接口，管理员额外拥有客服工作台接口，其余角色额外拥有系统总览接口
    /// </summary>
    /// <param name="roleCode">角色编码</param>
    /// <param name="basicEndpoints">基础接口</param>
    /// <param name="dashboardEndpoints">系统总览接口</param>
    /// <param name="serviceEndpoints">客服工作台接口</param>
    /// <returns>该角色应拥有的默认接口</returns>
    private static IEnumerable<ApiEndpoint> ResolveGrantedEndpoints(
        string roleCode
        , List<ApiEndpoint> basicEndpoints
        , List<ApiEndpoint> dashboardEndpoints
        , List<ApiEndpoint> serviceEndpoints
    ) {
        return roleCode switch
        {
            "R_USER" => basicEndpoints
            , "R_ADMIN" => basicEndpoints.Concat(dashboardEndpoints).Concat(serviceEndpoints)
            , _ => basicEndpoints.Concat(dashboardEndpoints)
        };
    }

    /// <summary>
    ///     撤销普通用户的系统总览菜单，兼容此前默认授予过该菜单的已有数据库
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <returns>异步撤销任务</returns>
    private static async Task RevokeUserConsoleMenuAsync(AppDbContext db) {
        var roleId = await db.Roles.Where(x => x.Code == "R_USER").Select(x => (long?)x.Id).SingleOrDefaultAsync().ConfigureAwait(false);
        var menuId = await db.Menus.Where(x => x.Name == "Console").Select(x => (long?)x.Id).SingleOrDefaultAsync().ConfigureAwait(false);
        if (roleId is null || menuId is null) {
            return;
        }

        _ = await db.RoleMenus.Where(x => x.RoleId == roleId && x.MenuId == menuId).ExecuteDeleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     撤销普通用户已有的指定接口授权，兼容此前默认授予过这些接口的已有数据库
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="endpoints">需要撤销的接口</param>
    /// <returns>异步撤销任务</returns>
    private static async Task RevokeUserEndpointsAsync(
        AppDbContext db
        , List<ApiEndpoint> endpoints
    ) {
        var roleId = await db.Roles.Where(x => x.Code == "R_USER").Select(x => (long?)x.Id).SingleOrDefaultAsync().ConfigureAwait(false);
        List<long> endpointIds = [.. endpoints.Select(x => x.Id)];
        if (roleId is null || endpointIds.Count == 0) {
            return;
        }

        _ = await db.RoleApis.Where(x => x.RoleId == roleId && endpointIds.Contains(x.ApiEndpointId)).ExecuteDeleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     初始化 SeedMenusAsync 方法对应的业务数据
    /// </summary>
    /// <param name="db">数据库上下文</param>
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

    /// <summary>
    ///     初始化 SeedRoleMenusAsync 方法对应的业务数据
    /// </summary>
    /// <param name="db">数据库上下文</param>
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

    /// <summary>
    ///     从接口表中按接口键筛选出对应的接口实体
    /// </summary>
    /// <param name="allEndpoints">全部接口实体</param>
    /// <param name="keys">接口键集合</param>
    /// <returns>匹配的接口实体</returns>
    private static List<ApiEndpoint> SelectEndpoints(
        IEnumerable<ApiEndpoint> allEndpoints
        , IReadOnlyList<string> keys
    ) {
        return [.. allEndpoints.Where(x => keys.Contains(ApiEndpointKey.Create(x.Method, x.Path)))];
    }
}