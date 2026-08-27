using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Data;

/// <summary>
///     系统数据种子初始化器
/// </summary>
internal static class SystemDataSeeder
{
    /// <summary>
    ///     初始化系统数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static async Task InitializeAsync(IServiceProvider services) {
        await DatabaseInitializer.InitializeSystemCoreAsync(services).ConfigureAwait(false);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var defaultDepartment = await EnsureDefaultDepartmentAsync(db).ConfigureAwait(false);
        if (!await db.Users.AnyAsync().ConfigureAwait(false)) {
            var superRole = await db.Roles.SingleAsync(x => x.Code == "R_SUPER").ConfigureAwait(false);
            var adminRole = await db.Roles.SingleAsync(x => x.Code == "R_ADMIN").ConfigureAwait(false);
            var userRole = await db.Roles.SingleAsync(x => x.Code == "R_USER").ConfigureAwait(false);
            var root = CreateUser("root", "root@aiadmin.local", "13800000000", superRole);
            var job = CreateUser("job", "job@aiadmin.local", "13800000003", superRole);
            var admin = CreateUser("admin", "admin@aiadmin.local", "13800000001", adminRole);
            var user = CreateUser("user", "user@aiadmin.local", "13800000002", userRole);
            await db.Users.AddRangeAsync(root, job, admin, user).ConfigureAwait(false);
            _ = await db.SaveChangesAsync().ConfigureAwait(false);
            await AddSeedUserDepartmentsAsync(db, defaultDepartment, root, job, admin, user).ConfigureAwait(false);
        }

        await EnsureWelcomeMessageAsync(db).ConfigureAwait(false);
    }

    private static async Task AddSeedUserDepartmentsAsync(
        AppDbContext db
        , Department defaultDepartment
        , params User[] users
    ) {
        foreach (var user in users) {
            var department = new Department { Name = user.UserName, Code = $"USER_{user.Id}", ParentId = defaultDepartment.Id, Sort = 0 };
            user.UserDepartments.Add(new UserDepartment { User = user, Department = department });
            _ = await db.Wallets.AddAsync(new Wallet { UserId = user.Id, OwnerDepartmentId = department.Id }).ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static User CreateUser(
        string name
        , string email
        , string phone
        , Role role
    ) {
        var user = new User
        {
            UserName = name
            , PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234qwer")
            , Email = email
            , Phone = phone
            , Gender = UserGender.Male
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        return user;
    }

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

    private static async Task EnsureWelcomeMessageAsync(AppDbContext db) {
        const string title = "Welcome to AiAdmin";
        if (await db.SystemMessages.AnyAsync(x => x.Title == title).ConfigureAwait(false)) {
            return;
        }

        var sender = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync().ConfigureAwait(false);
        if (sender is null) {
            return;
        }

        var message = new SystemMessage
        {
            SenderId = sender.Id, Title = title, Content = "<p>Welcome to AiAdmin. We hope you enjoy using the system.</p>"
        };
        var users = await db.Users.Where(x => x.IsEnabled).Select(x => x.Id).ToListAsync().ConfigureAwait(false);
        foreach (var userId in users) {
            message.Recipients.Add(new UserMessage { UserId = userId, Message = message });
        }

        _ = await db.SystemMessages.AddAsync(message).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
    }
}