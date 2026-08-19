using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 处理用户名密码登录并签发 JWT 访问令牌。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     提供用户登录接口
/// </summary>
[ApiController]
[ApiDescription("Authentication")]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    /// <summary>
    ///     校验账号密码并签发访问令牌
    /// </summary>
    /// <param name="request">登录凭据</param>
    /// <returns>登录结果或失败响应</returns>
    [HttpPost("login")]
    [ApiDescription("Sign in to the system")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> LoginAsync(LoginRequest request) {
        // 校验用户状态和密码，成功后返回带角色声明的令牌。
        var user = await db
            .Users.Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.UserName == request.UserName)
            .ConfigureAwait(false);
        return user?.IsEnabled != true || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)
            ? Unauthorized(new ApiResponse<object>(401, ApiMessages.Get(Request, "invalidCredentials"), null))
            : Ok(ApiResponse<LoginResult>.Ok(new LoginResult(tokenService.Create(user), string.Empty), ApiMessages.Get(Request, "loginSuccess")));
    }
}