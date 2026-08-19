using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/user")]
public sealed class UsersController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "R_SUPER,R_ADMIN")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> Create(SaveUserRequest request) {
        if (await db.Users.AnyAsync(x => x.UserName == request.UserName)) {
            return Conflict(new ApiResponse<object>(409, ApiMessages.Get(Request, "userExists"), null));
        }

        if (string.IsNullOrWhiteSpace(request.Password)) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "passwordRequired"), null));
        }

        var roles = await ResolveRoles(request.Roles);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "invalidRole"), null));
        }

        var user = new User
        {
            UserName = request.UserName.Trim()
            , NickName = string.IsNullOrWhiteSpace(request.NickName) ? request.UserName.Trim() : request.NickName.Trim()
            , PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            , Email = request.Email.Trim()
            , Phone = request.Phone.Trim()
            , Gender = request.Gender
            , IsEnabled = request.IsEnabled
        };
        foreach (var role in roles) {
            user.UserRoles.Add(new UserRole { User = user, Role = role });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), ApiMessages.Get(Request, "userCreated")));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "R_SUPER,R_ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id) {
        var currentId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        if (currentId == id) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "cannotDeleteSelf"), null));
        }

        var user = await db.Users.FindAsync(id);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, ApiMessages.Get(Request, "userNotFound"), null));
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }, ApiMessages.Get(Request, "userDeleted")));
    }

    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<CurrentUserResult>>> Info() {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == id);
        var roles = user.UserRoles.Select(x => x.Role.Code).ToArray();
        return Ok(
            ApiResponse<CurrentUserResult>.Ok(
                new CurrentUserResult(user.Id, user.UserName, user.Email, user.Avatar, roles, ["add", "edit", "delete"])
            )
        );
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<UserListItem>>>> List(
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

        var total = await query.CountAsync();
        var users = await query.OrderByDescending(x => x.Id).Skip((current - 1) * size).Take(size).ToListAsync();
        var items = users.Select(ToListItem).ToList();
        return Ok(ApiResponse<PagedResponse<UserListItem>>.Ok(new PagedResponse<UserListItem>(items, current, size, total)));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<object>>> Roles() {
        var roleEntities = await db.Roles.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var roles = roleEntities
            .Select(x => new
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
            )
            .ToList();
        return Ok(ApiResponse<object>.Ok(roles));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "R_SUPER,R_ADMIN")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> Update(
        long id
        , SaveUserRequest request
    ) {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, ApiMessages.Get(Request, "userNotFound"), null));
        }

        if (await db.Users.AnyAsync(x => x.UserName == request.UserName && x.Id != id)) {
            return Conflict(new ApiResponse<object>(409, ApiMessages.Get(Request, "userExists"), null));
        }

        var roles = await ResolveRoles(request.Roles);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, ApiMessages.Get(Request, "invalidRole"), null));
        }

        user.UserName = request.UserName.Trim();
        user.NickName = string.IsNullOrWhiteSpace(request.NickName) ? user.UserName : request.NickName.Trim();
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
        await db.SaveChangesAsync();
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), ApiMessages.Get(Request, "userUpdated")));
    }

    private static UserListItem ToListItem(User user) {
        return new UserListItem(
            user.Id, user.Avatar ?? string.Empty, user.IsEnabled ? "1" : "2", user.UserName, user.Gender, user.NickName, user.Phone, user.Email
            , [.. user.UserRoles.Select(x => x.Role.Code)], "system", user.CreatedAt, "system", user.UpdatedAt
        );
    }

    private async Task<List<Role>?> ResolveRoles(string[] codes) {
        var distinct = codes.Distinct().ToArray();
        var roles = await db.Roles.Where(x => distinct.Contains(x.Code)).ToListAsync();
        return roles.Count == distinct.Length ? roles : null;
    }
}