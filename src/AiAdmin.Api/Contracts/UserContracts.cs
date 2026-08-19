using System.ComponentModel.DataAnnotations;

namespace AiAdmin.Api.Contracts;

public sealed record UserListItem(
    long Id
    , string Avatar
    , string Status
    , string UserName
    , string UserGender
    , string NickName
    , string UserPhone
    , string UserEmail
    , string[] UserRoles
    , string CreateBy
    , DateTime CreateTime
    , string UpdateBy
    , DateTime UpdateTime);

public sealed class SaveUserRequest
{
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [StringLength(10)]
    public string Gender { get; init; } = "male";

    public bool IsEnabled { get; init; } = true;

    [StringLength(50)]
    public string NickName { get; init; } = string.Empty;

    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; init; }

    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [MinLength(1)]
    public string[] Roles { get; init; } = [];

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; init; } = string.Empty;
}