namespace AiAdmin.Api.Models;

public sealed class User
{
    public string? Avatar { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Email { get; set; } = string.Empty;
    public string Gender { get; set; } = "male";
    public long Id { get; init; }
    public bool IsEnabled { get; set; } = true;
    public required string NickName { get; set; }
    public required string PasswordHash { get; set; }
    public string Phone { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public required string UserName { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
}