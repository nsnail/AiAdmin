using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

public sealed record RoleListItem(long RoleId, string RoleName, string RoleCode, string Description, bool Enabled, DateTime CreateTime);

public sealed class SaveRoleRequest
{
    [StringLength(200)]
    public string Description { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string RoleCode { get; init; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string RoleName { get; init; } = string.Empty;
}