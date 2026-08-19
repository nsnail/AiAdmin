using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("sys_user");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.UserName).IsUnique();
                entity.HasIndex(x => x.Email);
                entity.Property(x => x.UserName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
                entity.Property(x => x.NickName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(100);
                entity.Property(x => x.Phone).HasMaxLength(20);
                entity.Property(x => x.Gender).HasMaxLength(10);
                entity.Property(x => x.Avatar).HasMaxLength(500);
            }
        );

        modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("sys_role");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(200);
                entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            }
        );

        modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("sys_user_role");
                entity.HasKey(x => new { x.UserId, x.RoleId });
                entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
                entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
            }
        );

        modelBuilder.Entity<RoleMenu>(entity =>
            {
                entity.ToTable("sys_role_menu");
                entity.HasKey(x => new { x.RoleId, x.MenuId });
                entity.HasOne(x => x.Role).WithMany(x => x.RoleMenus).HasForeignKey(x => x.RoleId);
                entity.HasOne(x => x.Menu).WithMany(x => x.RoleMenus).HasForeignKey(x => x.MenuId);
            }
        );

        modelBuilder.Entity<Menu>(entity =>
            {
                entity.ToTable("sys_menu");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Name).IsUnique();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Path).HasMaxLength(300).IsRequired();
                entity.Property(x => x.Component).HasMaxLength(300);
                entity.Property(x => x.ParentName).HasMaxLength(100);
                entity.Property(x => x.MetaJson).HasColumnType("TEXT");
            }
        );
    }
}