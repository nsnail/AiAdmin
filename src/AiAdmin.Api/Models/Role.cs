namespace AiAdmin.Api.Models;

public sealed class Role
{
    public required string Code { get; set; }
    public string Description { get; set; } = string.Empty;
    public long Id { get; init; }
    public bool IsEnabled { get; set; } = true;
    public required string Name { get; set; }
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; init; } = [];
}

public sealed class RoleMenu
{
    public Menu Menu { get; init; } = null!;
    public long MenuId { get; init; }
    public Role Role { get; init; } = null!;
    public long RoleId { get; init; }
}

public sealed class Menu
{
    public string Component { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public long Id { get; init; }
    public bool IsEnabled { get; set; } = true;
    public string MetaJson { get; set; } = "{}";
    public string Name { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ICollection<RoleMenu> RoleMenus { get; init; } = [];
    public int Sort { get; set; }
}

public sealed class UserRole
{
    public Role Role { get; init; } = null!;
    public long RoleId { get; init; }
    public User User { get; init; } = null!;
    public long UserId { get; init; }
}