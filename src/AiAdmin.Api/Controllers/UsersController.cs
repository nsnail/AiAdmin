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
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     用户管理控制器
/// </summary>
[ApiController]
[ApiDescription("User management")]
[Authorize]
[Route("api/user")]
public sealed class UsersController(AppDbContext db, MinioStorageService storage) : ControllerBase
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
        var userName = request.UserName.Trim();
        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.UserName == userName).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Username already exists", null));
        }

        if (await db.Users.AnyAsync(x => x.Email == email).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Email already exists", null));
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
            UserName = userName
            , PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            , Email = email
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

        var personalDepartment = new Department { Name = user.UserName, Code = $"USER_{user.Id}", ParentId = defaultDepartment.Id, Sort = 0 };
        user.UserDepartments.Add(new UserDepartment { User = user, Department = personalDepartment });
        _ = await db.Wallets.AddAsync(new Wallet { UserId = user.Id, OwnerDepartmentId = personalDepartment.Id }).ConfigureAwait(false);

        var departments = await ResolveDepartmentsAsync(request.DepartmentIds).ConfigureAwait(false);
        if (departments is null) {
            return BadRequest(new ApiResponse<object>(400, "One or more departments are invalid", null));
        }

        foreach (var department in departments) {
            user.UserDepartments.Add(new UserDepartment { User = user, Department = department });
        }

        // 与用户注册流程一致，事务确保用户、个人部门及关联数据同时创建成功
        await using var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);
        _ = await db.Users.AddAsync(user).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), "User created"));
    }

    /// <summary>
    ///     清空用户头像地址
    /// </summary>
    /// <param name="id">用户主键</param>
    /// <returns>更新后的用户列表项</returns>
    [HttpDelete("{id:long}/avatar")]
    [ApiDescription("Delete user avatar")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> DeleteAvatarAsync(long id) {
        var currentUserId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        if (id != currentUserId && !User.IsInRole("R_SUPER")) {
            return StatusCode(403, new ApiResponse<object>(403, "You can only update your own avatar", null));
        }

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

        user.Avatar = null;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), "Avatar deleted"));
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
        var sortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["userInfo"] = nameof(Models.User.UserName)
            , ["userName"] = nameof(Models.User.UserName)
            , ["userGender"] = nameof(Models.User.Gender)
            , ["userPhone"] = nameof(Models.User.Phone)
            , ["userEmail"] = nameof(Models.User.Email)
            , ["status"] = nameof(Models.User.IsEnabled)
            , ["isEnabled"] = nameof(Models.User.IsEnabled)
            , ["createTime"] = nameof(Models.User.CreatedAt)
        };
        var descending = string.Equals(request.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var sortedQuery = request.SortField?.ToLowerInvariant() switch
        {
            "userroles" => descending
                ? query.OrderByDescending(x => x.UserRoles.OrderBy(role => role.Role.Name).Select(role => role.Role.Name).FirstOrDefault())
                : query.OrderBy(x => x.UserRoles.OrderBy(role => role.Role.Name).Select(role => role.Role.Name).FirstOrDefault())
            , "departmentnames" => descending
                ? query.OrderByDescending(x =>
                    x
                        .UserDepartments.OrderBy(department => department.Department.Name)
                        .Select(department => department.Department.Name)
                        .FirstOrDefault()
                )
                : query.OrderBy(x =>
                    x
                        .UserDepartments.OrderBy(department => department.Department.Name)
                        .Select(department => department.Department.Name)
                        .FirstOrDefault()
                )
            , _ => query.ApplyDynamicSort(request.SortField, request.SortOrder, nameof(Models.User.CreatedAt), true, sortAliases)
        };
        var users = await sortedQuery.Skip((current - 1) * size).Take(size).ToListAsync().ConfigureAwait(false);
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
    /// <param name="request">用户修改请求</param>
    /// <returns>更新后的用户</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update user")]
    public async Task<ActionResult<ApiResponse<UserListItem>>> UpdateAsync(
        long id
        , UpdateUserRequest request
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

        var roles = await ResolveRolesAsync(request.Roles).ConfigureAwait(false);
        if (roles is null) {
            return BadRequest(new ApiResponse<object>(400, "One or more roles are invalid", null));
        }

        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.Gender = request.Gender;
        user.IsEnabled = request.IsEnabled;
        if (request.Avatar is not null) {
            user.Avatar = string.IsNullOrWhiteSpace(request.Avatar) ? null : request.Avatar.Trim();
        }
        else if (request.RemoveAvatar == true) {
            user.Avatar = null;
        }

        if (!string.IsNullOrWhiteSpace(request.Password)) {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        var departments = await ResolveDepartmentsAsync(request.DepartmentIds).ConfigureAwait(false);
        if (departments is null) {
            return BadRequest(new ApiResponse<object>(400, "One or more departments are invalid", null));
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = [.. roles.Select(role => new UserRole { User = user, Role = role })];
        var personalDepartment = user.UserDepartments.SingleOrDefault(x => x.Department.Code == $"USER_{user.Id}");
        var selectedDepartmentIds = departments.Select(x => x.Id).ToHashSet();
        var removedDepartments
            = user.UserDepartments.Where(x => x != personalDepartment && !selectedDepartmentIds.Contains(x.DepartmentId)).ToArray();
        db.UserDepartments.RemoveRange(removedDepartments);
        foreach (var removedDepartment in removedDepartments) {
            _ = user.UserDepartments.Remove(removedDepartment);
        }

        var existingDepartmentIds = user.UserDepartments.Select(x => x.DepartmentId).ToHashSet();
        foreach (var department in departments.Where(x => !existingDepartmentIds.Contains(x.Id))) {
            user.UserDepartments.Add(new UserDepartment { User = user, Department = department });
        }

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

    /// <summary>
    ///     上传并更新用户头像
    /// </summary>
    /// <param name="id">用户主键</param>
    /// <param name="file">头像图片文件</param>
    /// <returns>更新后的用户列表项</returns>
    [HttpPost("{id:long}/avatar")]
    [ApiDescription("Upload user avatar")]
    [RequestSizeLimit(512000)]
    public async Task<ActionResult<ApiResponse<UserListItem>>> UploadAvatarAsync(
        long id
        , IFormFile file
    ) {
        var currentUserId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        if (id != currentUserId && !User.IsInRole("R_SUPER")) {
            return StatusCode(403, new ApiResponse<object>(403, "You can only update your own avatar", null));
        }

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

        switch (file.Length) {
            case 0:
                return BadRequest(new ApiResponse<object>(400, "Avatar file is empty", null));
            case > 512000:
                return BadRequest(new ApiResponse<object>(400, "Avatar file must not exceed 500 KB", null));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"];
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
            return BadRequest(new ApiResponse<object>(400, "Avatar must be a supported image format", null));
        }

        var objectName = $"avatars/{id}.png";
        await using var input = file.OpenReadStream();
        using var image = await ImageSharpImage.LoadAsync(input).ConfigureAwait(false);
        image.Mutate(context =>
            context
                .Resize(new ResizeOptions { Size = new Size(120, 120), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center })
                .BackgroundColor(Color.White)
        );
        await using var output = new MemoryStream();
        await image.SaveAsync(output, new PngEncoder()).ConfigureAwait(false);
        output.Position = 0;
        await storage.UploadAsync(objectName, output, output.Length, "image/png").ConfigureAwait(false);
        user.Avatar = storage.GetPreviewUrl(objectName);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<UserListItem>.Ok(ToListItem(user), "Avatar uploaded"));
    }

    private static CurrentUserResult ToCurrentUserResult(User user) {
        return new CurrentUserResult(
            user.Id, user.UserName, user.Email, user.Phone, user.Gender, user.Avatar, [.. user.UserRoles.Select(x => x.Role.Code)]
            , ["add", "edit", "delete"]
        );
    }

    private static UserListItem ToListItem(User user) {
        return new UserListItem(
            user.Id, user.Avatar ?? string.Empty, user.IsEnabled ? "1" : "2", user.UserName, user.Gender, user.Phone, user.Email, user.IsEnabled
            , [.. user.UserRoles.Select(x => x.Role.Code)], [.. user.UserDepartments.Select(x => x.DepartmentId)]
            , [.. user.UserDepartments.Select(x => x.Department.Name)], "system", ServerTime.ToOffset(user.CreatedAt), "system"
            , user.UpdatedAt is { } updatedAt ? ServerTime.ToOffset(updatedAt) : null
        );
    }

    /// <summary>
    ///     校验并加载用户选择的部门
    /// </summary>
    /// <param name="ids">部门主键集合</param>
    /// <returns>有效部门集合，无效时返回空值</returns>
    private async Task<List<Department>?> ResolveDepartmentsAsync(long[] ids) {
        var distinct = ids.Distinct().ToArray();
        var departments = await db.Departments.Where(x => distinct.Contains(x.Id)).ToListAsync().ConfigureAwait(false);
        return departments.Count == distinct.Length ? departments : null;
    }

    private async Task<List<Role>?> ResolveRolesAsync(string[] codes) {
        var distinct = codes.Distinct().ToArray();
        var roles = await db.Roles.Where(x => distinct.Contains(x.Code)).ToListAsync().ConfigureAwait(false);
        return roles.Count == distinct.Length ? roles : null;
    }
}