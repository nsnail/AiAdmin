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
    ///     修改当前登录用户密码
    /// </summary>
    /// <param name="request">密码修改请求</param>
    /// <returns>密码修改结果</returns>
    [HttpPut("password")]
    [ApiDescription("Change current user password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePasswordAsync(ChangePasswordRequest request) {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (user is null) {
            return Unauthorized(new ApiResponse<object>(401, "Login session has expired, please log in again", null));
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)) {
            return BadRequest(new ApiResponse<object>(400, "Current password is incorrect", null));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Password changed"));
    }

    /// <summary>
    ///     创建用户
    /// </summary>
    /// <param name="request">用户保存请求</param>
    /// <returns>创建后的用户</returns>
    [HttpPost]
    [ApiDescription("Create user")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> CreateAsync(SaveUserRequest request) {
        if (await db.Users.AnyAsync(x => x.UserName == request.UserName).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Username already exists", null));
        }

        if (string.IsNullOrWhiteSpace(request.Password)) {
            return BadRequest(new ApiResponse<object>(400, "Password is required for a new user", null));
        }

        var roles = await ResolveRolesAsync(request.Roles).ConfigureAwait(false);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, "One or more roles are invalid", null));
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

        var defaultDepartment = await db.Departments.SingleOrDefaultAsync(x => x.Code == Department.DEFAULT_CODE).ConfigureAwait(false);
        if (defaultDepartment is null) {
            return StatusCode(500, new ApiResponse<object>(500, "Default department does not exist", null));
        }

        var personalDepartment = new Department { Name = user.UserName, Code = $"USER_{user.Id}", ParentId = defaultDepartment.Id };
        user.UserDepartments.Add(new UserDepartment { User = user, Department = personalDepartment });
        _ = await db.Users.AddAsync(user).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), "User created"));
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
            return BadRequest(new ApiResponse<object>(400, "You cannot delete your own account", null));
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, "User not found", null));
        }

        // 删除用户时解除其发起和接收的邀请关系，保留其他用户账号
        _ = await db.UserReferrals.Where(x => x.OwnerId == id || x.InviteeUserId == id).ExecuteDeleteAsync().ConfigureAwait(false);
        _ = db.Users.Remove(user);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "User deleted"));
    }

    /// <summary>
    ///     查询用户列表筛选字段元数据
    /// </summary>
    /// <returns>用户筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query user filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<User>()));
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
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        return user is null
            ? Unauthorized(new ApiResponse<object>(401, "Login session has expired, please log in again", null))
            : Ok(ApiResponse<CurrentUserResult>.Ok(ToCurrentUserResult(user)));
    }

    /// <summary>
    ///     分页查询用户
    /// </summary>
    /// <param name="request">包含动态筛选和分页信息的请求体</param>
    /// <returns>用户分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query user list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<UserListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var current = request.Current;
        var size = request.Size;
        var query = db
            .Users.AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.UserDepartments)
            .ThenInclude(x => x.Department)
            .ApplyDynamicFilter(request.DynamicFilter);

        var total = await query.CountAsync().ConfigureAwait(false);
        var users = await query.OrderByDescending(x => x.Id).Skip((current - 1) * size).Take(size).ToListAsync().ConfigureAwait(false);
        var items = users.ConvertAll(ToListItem);
        return Ok(ApiResponse<PagedResponse<UserListItem>>.Ok(new PagedResponse<UserListItem>(items, current, size, total)));
    }

    /// <summary>
    ///     查询当前用户的全部下级邀请关系树
    /// </summary>
    /// <returns>当前用户邀请码和下级邀请关系树</returns>
    [HttpGet("referrals")]
    [ApiDescription("Query current user referral tree")]
    public async Task<ActionResult<ApiResponse<ReferralTreeResult>>> ReferralsAsync() {
        var currentUserId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var invitationCode = await db
            .Users.Where(x => x.Id == currentUserId)
            .Select(x => x.InvitationCode)
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (invitationCode is null) {
            return Unauthorized(new ApiResponse<object>(401, "Login session has expired, please log in again", null));
        }

        var referrals = await db
            .UserReferrals.AsNoTracking()
            .Include(x => x.Invitee)
            .Select(x => new
                {
                    InviterId = x.OwnerId
                    , x.Invitee.Id
                    , x.Invitee.UserName
                    , x.Invitee.Email
                    , x.Invitee.InvitationCode
                    , x.Invitee.CreatedAt
                }
            )
            .ToListAsync()
            .ConfigureAwait(false);
        var usersByInviter = referrals.ToLookup(x => x.InviterId);

        return Ok(
            ApiResponse<ReferralTreeResult>.Ok(new ReferralTreeResult { InvitationCode = invitationCode, Children = BuildChildren(currentUserId) })
        );

        // 邀请关系只在注册时指向已有用户，因此可以从当前用户安全递归构建任意深度的后代树
        IReadOnlyList<ReferralTreeItem> BuildChildren(long inviterId) {
            return
            [
                .. usersByInviter[inviterId]
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new ReferralTreeItem
                        {
                            Id = x.Id
                            , UserName = x.UserName
                            , Email = x.Email
                            , InvitationCode = x.InvitationCode
                            , CreatedAt = ServerTime.ToOffset(x.CreatedAt)
                            , Children = BuildChildren(x.Id)
                        }
                    )
            ];
        }
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
                    "R_SUPER" => "Super administrator"
                    , "R_ADMIN" => "Administrator"
                    , "R_USER" => "User"
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
        var user = await db
            .Users.Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.UserDepartments)
            .ThenInclude(x => x.Department)
            .SingleOrDefaultAsync(x => x.Id == id)
            .ConfigureAwait(false);
        if (user is null) {
            return NotFound(new ApiResponse<object>(404, "User not found", null));
        }

        if (await db.Users.AnyAsync(x => x.UserName == request.UserName && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Username already exists", null));
        }

        var roles = await ResolveRolesAsync(request.Roles).ConfigureAwait(false);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, "One or more roles are invalid", null));
        }

        user.UserName = request.UserName.Trim();
        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.Gender = request.Gender;
        user.IsEnabled = request.IsEnabled;
        if (!string.IsNullOrWhiteSpace(request.Password)) {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = [.. roles.Select(role => new UserRole { User = user, Role = role })];
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), "User updated"));
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
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (user is null) {
            return Unauthorized(new ApiResponse<object>(401, "Login session has expired, please log in again", null));
        }

        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.Gender = request.Gender;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<CurrentUserResult>.Ok(ToCurrentUserResult(user), "Profile updated"));
    }

    private static CurrentUserResult ToCurrentUserResult(User user) {
        return new CurrentUserResult(
            user.Id, user.UserName, user.Email, user.Phone, user.Gender, user.Avatar, [.. user.UserRoles.Select(x => x.Role.Code)]
            , ["add", "edit", "delete"]
        );
    }

    private static UserListItem ToListItem(User user) {
        return new UserListItem(
            user.Id, user.Avatar ?? string.Empty, user.IsEnabled ? "1" : "2", user.UserName, user.Gender, user.Phone, user.Email
            , [.. user.UserRoles.Select(x => x.Role.Code)], [.. user.UserDepartments.Select(x => x.DepartmentId)]
            , [.. user.UserDepartments.Select(x => x.Department.Name)], "system", ServerTime.ToOffset(user.CreatedAt), "system"
            , user.UpdatedAt is { } updatedAt ? ServerTime.ToOffset(updatedAt) : null
        );
    }

    private async Task<List<Role>?> ResolveRolesAsync(string[] codes) {
        var distinct = codes.Distinct().ToArray();
        var roles = await db.Roles.Where(x => distinct.Contains(x.Code)).ToListAsync().ConfigureAwait(false);
        return roles.Count == distinct.Length ? roles : null;
    }
}