using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Menu = AiAdmin.Api.Models.Menu;

// 配置业务实体与数据库表、索引及关联关系的映射。
namespace AiAdmin.Api.Data;

/// <summary>
///     应用数据库上下文
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, DataScopeContext dataScope, IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    /// <summary>
    ///     接口实体集合
    /// </summary>
    public DbSet<ApiEndpoint> ApiEndpoints => Set<ApiEndpoint>();

    /// <summary>
    ///     当前数据库操作审计用户主键
    /// </summary>
    public long? CurrentAuditActorUserId => dataScope.IsInitialized ? dataScope.UserId : null;

    /// <summary>
    ///     数据库写入审计日志集合
    /// </summary>
    public DbSet<DatabaseAuditLog> DatabaseAuditLogs => Set<DatabaseAuditLog>();

    /// <summary>
    ///     部门实体集合
    /// </summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>
    ///     字典目录实体集合
    /// </summary>
    public DbSet<DictionaryCategory> DictionaryCategories => Set<DictionaryCategory>();

    /// <summary>
    ///     字典内容实体集合
    /// </summary>
    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();

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
    ///     用户部门关联集合
    /// </summary>
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();

    /// <summary>
    ///     用户邀请关系实体集合
    /// </summary>
    public DbSet<UserReferral> UserReferrals => Set<UserReferral>();

    /// <summary>
    ///     用户角色关联集合
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    ///     用户实体集合
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    ///     保存实体变更并自动维护审计时间
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">保存成功后是否接受所有变更</param>
    /// <returns>写入数据库的状态条目数</returns>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        PrepareSaveChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    ///     异步保存实体变更并自动维护审计时间
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">保存成功后是否接受所有变更</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>写入数据库的状态条目数</returns>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess
        , CancellationToken cancellationToken = default
    ) {
        PrepareSaveChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    ///     配置实体关系、表名和索引
    /// </summary>
    /// <param name="modelBuilder">实体模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        _ = modelBuilder.Entity<User>(entity =>
            {
                _ = entity.ToTable("sys_user");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.UserName).IsUnique();
                _ = entity.HasIndex(x => x.Email);
                _ = entity.HasIndex(x => x.InvitationCode).IsUnique();
                _ = entity.Property(x => x.UserName).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.InvitationCode).HasMaxLength(12).IsRequired();
                _ = entity.Property(x => x.Email).HasMaxLength(100);
                _ = entity.Property(x => x.Phone).HasMaxLength(20);
                _ = entity.Property(x => x.Gender).HasMaxLength(10);
                _ = entity.Property(x => x.Avatar).HasMaxLength(500);
            }
        );

        _ = modelBuilder.Entity<Department>(entity =>
            {
                _ = entity.ToTable("sys_department");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.Code).IsUnique();
                _ = entity.HasIndex(x => x.ParentId);
                _ = entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Leader).HasMaxLength(50);
                _ = entity.Property(x => x.Phone).HasMaxLength(20);
                _ = entity.Property(x => x.Email).HasMaxLength(100).IsRequired(false);
                _ = entity.Property(x => x.IsEnabled).HasDefaultValue(true);
                _ = entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            }
        );

        _ = modelBuilder.Entity<UserDepartment>(entity =>
            {
                _ = entity.ToTable("sys_user_department");
                _ = entity.HasKey(x => new { x.UserId, x.DepartmentId });
                _ = entity.HasOne(x => x.User).WithMany(x => x.UserDepartments).HasForeignKey(x => x.UserId);
                _ = entity.HasOne(x => x.Department).WithMany(x => x.UserDepartments).HasForeignKey(x => x.DepartmentId);
            }
        );

        _ = modelBuilder.Entity<UserReferral>(entity =>
            {
                _ = entity.ToTable("sys_user_referral");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.OwnerId);
                _ = entity.HasIndex(x => x.OwnerDepartmentId);
                _ = entity.HasIndex(x => x.InviteeUserId).IsUnique();
                _ = entity.HasOne(x => x.Invitee).WithMany().HasForeignKey(x => x.InviteeUserId).OnDelete(DeleteBehavior.Restrict);
                _ = entity.HasQueryFilter(x =>
                    !dataScope.IsInitialized
                    || dataScope.HasAllData
                    || (dataScope.HasSelfData && x.OwnerId == dataScope.UserId)
                    || dataScope.DepartmentIds.Contains(x.OwnerDepartmentId)
                );
            }
        );

        _ = modelBuilder.Entity<Role>(entity =>
            {
                _ = entity.ToTable("sys_role");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.Code).IsUnique();
                _ = entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                _ = entity.Property(x => x.Description).HasMaxLength(200);
                _ = entity.Property(x => x.DataScope).HasMaxLength(50).HasDefaultValue(RoleDataScope.SELF);
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
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
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
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.Name).IsUnique();
                _ = entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Path).HasMaxLength(300).IsRequired();
                _ = entity.Property(x => x.Component).HasMaxLength(300);
                _ = entity.Property(x => x.ParentName).HasMaxLength(100);
                _ = entity.Property(x => x.MetaJson).HasColumnType("TEXT");
            }
        );

        _ = modelBuilder.Entity<DictionaryCategory>(entity =>
            {
                _ = entity.ToTable("sys_dict_category");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.Code).IsUnique();
                _ = entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                _ = entity.HasOne<DictionaryCategory>().WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            }
        );

        _ = modelBuilder.Entity<DictionaryItem>(entity =>
            {
                _ = entity.ToTable("sys_dict_item");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => new { x.CategoryId, x.Label }).IsUnique();
                _ = entity.Property(x => x.Value).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Label).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.Remark).HasMaxLength(500);
                _ = entity.HasOne(x => x.Category).WithMany(x => x.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            }
        );

        _ = modelBuilder.Entity<DatabaseAuditLog>(entity =>
            {
                _ = entity.ToTable("sys_database_audit_log");
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.HasIndex(x => x.ActorUserId);
                _ = entity.HasIndex(x => x.CreatedAt);
                _ = entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
                _ = entity.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
                _ = entity.Property(x => x.Operation).HasMaxLength(20).IsRequired();
                _ = entity.Property(x => x.Method).HasMaxLength(20).IsRequired();
                _ = entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            }
        );
    }

    /// <summary>
    ///     为本次数据库写入创建审计日志
    /// </summary>
    /// <param name="entries">本次保存的实体状态条目</param>
    private void AddDatabaseAuditLogs(IReadOnlyList<EntityEntry> entries) {
        var request = httpContextAccessor.HttpContext?.Request;
        long? actorUserId = dataScope.IsInitialized ? dataScope.UserId : null;
        foreach (var entry in entries) {
            var primaryKey = entry.Metadata.FindPrimaryKey();
            var entityId = primaryKey is null
                ? string.Empty
                : string.Join(",", primaryKey.Properties.Select(x => entry.Property(x.Name).CurrentValue?.ToString() ?? string.Empty));
            _ = DatabaseAuditLogs.Add(
                new DatabaseAuditLog
                {
                    ActorUserId = actorUserId
                    , EntityName = entry.Metadata.ClrType.Name
                    , EntityId = entityId
                    , Operation = entry.State.ToString()
                    , Method = request?.Method ?? "SYSTEM"
                    , Path = request?.Path.Value ?? "SYSTEM"
                }
            );
        }
    }

    /// <summary>
    ///     为新增的受管控实体补齐当前操作者和所属部门
    /// </summary>
    /// <param name="entries">本次保存的实体状态条目</param>
    /// <exception cref="DataAccessDeniedException">当前用户无权操作目标实体时引发</exception>
    private void AssignOwners(IReadOnlyList<EntityEntry> entries) {
        if (!dataScope.IsInitialized || dataScope.UserId == 0) {
            return;
        }

        foreach (var entry in entries.Where(x => x is { State: EntityState.Added, Entity: IOwner })) {
            var owner = (IOwner)entry.Entity;
            if (owner.OwnerId == 0) {
                owner.OwnerId = dataScope.UserId;
            }

            if (owner.OwnerDepartmentId == 0) {
                // 用户可能同时加入多个部门，所有者部门统一使用 USER_用户ID 对应的主部门
                owner.OwnerDepartmentId = dataScope.DefaultOwnerDepartmentId;
            }
        }

        if (entries.Where(x => x.Entity is IOwner).Any(entry => !dataScope.CanAccess((IOwner)entry.Entity))) {
            throw new DataAccessDeniedException();
        }
    }

    /// <summary>
    ///     根据实体状态维护创建时间和最后更新时间
    /// </summary>
    private void PrepareSaveChanges() {
        var entries = ChangeTracker
            .Entries()
            .Where(x => x.Entity is not DatabaseAuditLog && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        AssignOwners(entries);
        AddDatabaseAuditLogs(entries);
        UpdateAuditTimes();
    }

    /// <summary>
    ///     根据实体状态维护创建时间和最后更新时间
    /// </summary>
    private void UpdateAuditTimes() {
        var now = ServerTime.Now;
        foreach (var entry in ChangeTracker.Entries<EntityBase>()) {
            switch (entry.State) {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null;
                    break;
                case EntityState.Modified:
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}