using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;
using Menu = AiAdmin.Api.Models.Menu;

// 配置业务实体与数据库表、索引及关联关系的映射。
namespace AiAdmin.Api.Data;

/// <summary>
///     应用数据库上下文
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    ///     接口实体集合
    /// </summary>
    public DbSet<ApiEndpoint> ApiEndpoints => Set<ApiEndpoint>();

    /// <summary>
    ///     菜单实体集合
    /// </summary>
    public DbSet<Menu> Menus => Set<Menu>();

    /// <summary>
    ///     角色接口关联集合
    /// </summary>
    public DbSet<RoleApi> RoleApis => Set<RoleApi>();

    /// <summary>
    ///     角色菜单关联集合
    /// </summary>
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();

    /// <summary>
    ///     角色实体集合
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    ///     用户实体集合
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    ///     配置实体关系、表名和索引
    /// </summary>
    /// <param name="modelBuilder">实体模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        _ = modelBuilder.Entity<User>(entity =>
            {
                _ = entity.ToTable("sys_user");
                _ = entity.HasKey(x => x.Id);
                _ = entity.HasIndex(x => x.UserName).IsUnique();
                _ = entity.HasIndex(x => x.Email);
                _ = entity.Property(x => x.UserName).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.NickName).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Email).HasMaxLength(100);
                _ = entity.Property(x => x.Phone).HasMaxLength(20);
                _ = entity.Property(x => x.Gender).HasMaxLength(10);
                _ = entity.Property(x => x.Avatar).HasMaxLength(500);
            }
        );

        _ = modelBuilder.Entity<Role>(entity =>
            {
                _ = entity.ToTable("sys_role");
                _ = entity.HasKey(x => x.Id);
                _ = entity.HasIndex(x => x.Code).IsUnique();
                _ = entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Description).HasMaxLength(200);
                _ = entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            }
        );

        _ = modelBuilder.Entity<UserRole>(entity =>
            {
                _ = entity.ToTable("sys_user_role");
                _ = entity.HasKey(x => new { x.UserId, x.RoleId });
                _ = entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
                _ = entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
            }
        );

        _ = modelBuilder.Entity<RoleMenu>(entity =>
            {
                _ = entity.ToTable("sys_role_menu");
                _ = entity.HasKey(x => new { x.RoleId, x.MenuId });
                _ = entity.HasOne(x => x.Role).WithMany(x => x.RoleMenus).HasForeignKey(x => x.RoleId);
                _ = entity.HasOne(x => x.Menu).WithMany(x => x.RoleMenus).HasForeignKey(x => x.MenuId);
            }
        );

        _ = modelBuilder.Entity<ApiEndpoint>(entity =>
            {
                _ = entity.ToTable("sys_api");
                _ = entity.HasKey(x => x.Id);
                _ = entity.HasIndex(x => new { x.Method, x.Path }).IsUnique();
                _ = entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
                _ = entity.Property(x => x.AllowAnonymous).HasDefaultValue(false);
                _ = entity.Property(x => x.Method).HasMaxLength(20).IsRequired();
                _ = entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
                _ = entity.Property(x => x.Controller).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.ControllerName).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            }
        );

        _ = modelBuilder.Entity<RoleApi>(entity =>
            {
                _ = entity.ToTable("sys_role_api");
                _ = entity.HasKey(x => new { x.RoleId, x.ApiEndpointId });
                _ = entity.HasOne(x => x.Role).WithMany(x => x.RoleApis).HasForeignKey(x => x.RoleId);
                _ = entity.HasOne(x => x.ApiEndpoint).WithMany(x => x.RoleApis).HasForeignKey(x => x.ApiEndpointId);
            }
        );

        _ = modelBuilder.Entity<Menu>(entity =>
            {
                _ = entity.ToTable("sys_menu");
                _ = entity.HasKey(x => x.Id);
                _ = entity.HasIndex(x => x.Name).IsUnique();
                _ = entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Path).HasMaxLength(300).IsRequired();
                _ = entity.Property(x => x.Component).HasMaxLength(300);
                _ = entity.Property(x => x.ParentName).HasMaxLength(100);
                _ = entity.Property(x => x.MetaJson).HasColumnType("TEXT");
            }
        );
    }
}