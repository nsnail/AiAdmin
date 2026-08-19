namespace AiAdmin.Api.Models;

public sealed class Role
{
    public required string Code { get; set; }
    public string Description { get; set; } = string.Empty;
    public long Id { get; set; }
    public required string Name { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

public sealed class UserRole
{
    public Role Role { get; set; } = null!;
    public long RoleId { get; set; }
    public User User { get; set; } = null!;
    public long UserId { get; set; }
}