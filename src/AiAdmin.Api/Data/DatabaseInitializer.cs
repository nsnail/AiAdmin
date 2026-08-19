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
    }
}