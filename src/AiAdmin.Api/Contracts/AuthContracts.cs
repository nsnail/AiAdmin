namespace AiAdmin.Api.Contracts;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResult(string Token, string RefreshToken);

public sealed record CurrentUserResult(long UserId, string UserName, string Email, string? Avatar, string[] Roles, string[] Buttons);