using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> Login(LoginRequest request) {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.UserName == request.UserName);
        if (user is null || !user.IsEnabled || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) {
            return Unauthorized(new ApiResponse<object>(401, ApiMessages.Get(Request, "invalidCredentials"), null));
        }

        return Ok(ApiResponse<LoginResult>.Ok(new LoginResult(tokenService.Create(user), string.Empty), ApiMessages.Get(Request, "loginSuccess")));
    }
}