using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 提供用户增删改查和当前用户信息查询。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     用户管理控制器
/// </summary>
[ApiController]
[ApiDescription("User management")]
[Authorize]
[Route("api/user")]
public sealed class UsersController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     创建用户
    /// </summary>
    /// <param name="request">用户保存请求</param>
    /// <returns>创建后的用户</returns>
    [HttpPost]
    [ApiDescription("Create user")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> CreateAsync(SaveUserRequest request) {
        if (await db.Users.AnyAsync(x => x.UserName == request.UserName).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, ApiMessages.Get(Request, "userExists"), null));
        }

        if (string.IsNullOrWhiteSpace(request.Password)) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "passwordRequired"), null));
        }

        var roles = await ResolveRolesAsync(request.Roles).ConfigureAwait(false);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "invalidRole"), null));
        }

        var user = new User
        {
            UserName = request.UserName.Trim()
            , PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            , Email = request.Email.Trim()
            , Phone = request.Phone.Trim()
            , Gender = request.Gender
            , IsEnabled = request.IsEnabled
        };
        foreach (var role in roles) {
            user.UserRoles.Add(new UserRole { User = user, Role = role });
        }

        _ = await db.Users.AddAsync(user).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), ApiMessages.Get(Request, "userCreated")));
    }

    /// <summary>
    ///     删除用户
    /// </summary>
    /// <param name="id">用户主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete user")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var currentId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        if (currentId == id) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "cannotDeleteSelf"), null));
        }

        var user = await db.Users.FindAsync(id).ConfigureAwait(false);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, ApiMessages.Get(Request, "userNotFound"), null));
        }

        _ = db.Users.Remove(user);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, ApiMessages.Get(Request, "userDeleted")));
    }

    /// <summary>
    ///     查询当前登录用户信息
    /// </summary>
    /// <returns>当前用户信息</returns>
    [HttpGet("info")]
    [ApiDescription("Get current user information")]
    public async Task<ActionResult<ApiResponse<CurrentUserResult>>> InfoAsync() {
        // 返回当前登录用户及其角色、按钮权限信息。
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == id).ConfigureAwait(false);
        return Ok(ApiResponse<CurrentUserResult>.Ok(ToCurrentUserResult(user)));
    }

    /// <summary>
    ///     修改当前登录用户密码
    /// </summary>
    /// <param name="request">密码修改请求</param>
    /// <returns>密码修改结果</returns>
    [HttpPut("password")]
    [ApiDescription("Change current user password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePasswordAsync(ChangePasswordRequest request) {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await db.Users.SingleAsync(x => x.Id == id).ConfigureAwait(false);
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "currentPasswordInvalid"), null));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, ApiMessages.Get(Request, "passwordChanged")));
    }

    /// <summary>
    ///     更新当前登录用户资料
    /// </summary>
    /// <param name="request">个人资料更新请求</param>
    /// <returns>更新后的当前用户信息</returns>
    [HttpPut("profile")]
    [ApiDescription("Update current user profile")]
    public async Task<ActionResult<ApiResponse<CurrentUserResult>>> UpdateProfileAsync(UpdateCurrentUserProfileRequest request) {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == id).ConfigureAwait(false);
        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.Gender = request.Gender;
        user.UpdatedAt = DateTime.UtcNow;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<CurrentUserResult>.Ok(ToCurrentUserResult(user), ApiMessages.Get(Request, "profileUpdated")));
    }

    /// <summary>
    ///     分页查询用户
    /// </summary>
    /// <param name="current">当前页码</param>
    /// <param name="size">每页记录数</param>
    /// <param name="userName">用户名筛选</param>
    /// <param name="userPhone">手机号筛选</param>
    /// <param name="userEmail">邮箱筛选</param>
    /// <param name="userGender">性别筛选</param>
    /// <param name="status">状态筛选</param>
    /// <returns>用户分页结果</returns>
    [HttpGet("list")]
    [ApiDescription("Query user list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<UserListItem>>>> ListAsync(
        [FromQuery] int current = 1
        , [FromQuery] int size = 20
        , [FromQuery] string? userName = null
        , [FromQuery] string? userPhone = null
        , [FromQuery] string? userEmail = null
        , [FromQuery] string? userGender = null
        , [FromQuery] string? status = null
    ) {
        current = Math.Max(current, 1);
        size = Math.Clamp(size, 1, 100);
        var query = db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(userName)) {
            query = query.Where(x => x.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(userPhone)) {
            query = query.Where(x => x.Phone.Contains(userPhone));
        }

        if (!string.IsNullOrWhiteSpace(userEmail)) {
            query = query.Where(x => x.Email.Contains(userEmail));
        }

        if (!string.IsNullOrWhiteSpace(userGender)) {
            query = query.Where(x => x.Gender == userGender);
        }

        query = status switch
        {
            "1" => query.Where(x => x.IsEnabled)
            , "2" => query.Where(x => !x.IsEnabled)
            , _ => query
        };

        var total = await query.CountAsync().ConfigureAwait(false);
        var users = await query.OrderByDescending(x => x.Id).Skip((current - 1) * size).Take(size).ToListAsync().ConfigureAwait(false);
        var items = users.ConvertAll(ToListItem);
        return Ok(ApiResponse<PagedResponse<UserListItem>>.Ok(new PagedResponse<UserListItem>(items, current, size, total)));
    }

    /// <summary>
    ///     查询可分配角色
    /// </summary>
    /// <returns>角色选项集合</returns>
    [HttpGet("roles")]
    [ApiDescription("Query assignable roles")]
    public async Task<ActionResult<ApiResponse<object>>> RolesAsync() {
        var roleEntities = await db.Roles.AsNoTracking().OrderBy(x => x.Id).ToListAsync().ConfigureAwait(false);
        var roles = roleEntities.ConvertAll(x => new
            {
                roleId = x.Id
                , roleName = x.Code switch
                {
                    "R_SUPER" => ApiMessages.Get(Request, "roleSuper")
                    , "R_ADMIN" => ApiMessages.Get(Request, "roleAdmin")
                    , "R_USER" => ApiMessages.Get(Request, "roleUser")
                    , _ => x.Name
                }
                , roleCode = x.Code
                , x.Description
            }
        );
        return Ok(ApiResponse<object>.Ok(roles));
    }

    /// <summary>
    ///     更新用户
    /// </summary>
    /// <param name="id">用户主键</param>
    /// <param name="request">用户保存请求</param>
    /// <returns>更新后的用户</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update user")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> UpdateAsync(
        long id
        , SaveUserRequest request
    ) {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, ApiMessages.Get(Request, "userNotFound"), null));
        }

        if (await db.Users.AnyAsync(x => x.UserName == request.UserName && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, ApiMessages.Get(Request, "userExists"), null));
        }

        var roles = await ResolveRolesAsync(request.Roles).ConfigureAwait(false);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "invalidRole"), null));
        }

        user.UserName = request.UserName.Trim();
        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.Gender = request.Gender;
        user.IsEnabled = request.IsEnabled;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Password)) {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = [.. roles.Select(role => new UserRole { User = user, Role = role })];
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), ApiMessages.Get(Request, "userUpdated")));
    }

    private static UserListItem ToListItem(User user) {
        return new UserListItem(
            user.Id, user.Avatar ?? string.Empty, user.IsEnabled ? "1" : "2", user.UserName, user.Gender, user.Phone, user.Email
            , [.. user.UserRoles.Select(x => x.Role.Code)], "system", user.CreatedAt, "system", user.UpdatedAt
        );
    }

    private static CurrentUserResult ToCurrentUserResult(User user) {
        return new CurrentUserResult(
            user.Id, user.UserName, user.Email, user.Phone, user.Gender, user.Avatar
            , [.. user.UserRoles.Select(x => x.Role.Code)], ["add", "edit", "delete"]
        );
    }

    private async Task<List<Role>?> ResolveRolesAsync(string[] codes) {
        var distinct = codes.Distinct().ToArray();
        var roles = await db.Roles.Where(x => distinct.Contains(x.Code)).ToListAsync().ConfigureAwait(false);
        return roles.Count == distinct.Length ? roles : null;
    }
}