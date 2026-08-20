using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MimeKit;
using MimeKit.Utils;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     提供用户登录接口
/// </summary>
[ApiController]
[ApiDescription("Authentication")]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, TokenService tokenService, IDistributedCache cache, ILogger<AuthController> logger)
    : ControllerBase
{
    private const int _PROOF_DIFFICULTY = 4;
    private const int _PUZZLE_HEIGHT = 160;
    private const int _PUZZLE_PIECE_SIZE = 44;
    private const int _PUZZLE_WIDTH = 320;
    private static readonly TimeSpan _challengeLifetime = TimeSpan.FromMinutes(2);

    private static readonly Action<ILogger, string, string, string, Exception?> _logSmtpAccepted = LoggerMessage.Define<string, string, string>(
        LogLevel.Information, new EventId(1001, "SmtpAccepted"), "SMTP accepted registration email {MessageId} for {Recipient}: {Response}"
    );

    /// <summary>
    ///     创建登录算力挑战
    /// </summary>
    /// <returns>算力挑战和难度</returns>
    [HttpGet("challenge")]
    [AllowAnonymous]
    [ApiDescription("Create login proof challenge")]
    public async Task<ActionResult<ApiResponse<LoginChallengeResult>>> ChallengeEndpointAsync() {
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await cache
            .SetStringAsync(
                $"login-proof:{challenge}", "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _challengeLifetime }
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<LoginChallengeResult>.Ok(new LoginChallengeResult(challenge, _PROOF_DIFFICULTY)));
    }

    /// <summary>
    ///     查询登录页面配置
    /// </summary>
    /// <returns>登录页面配置</returns>
    [HttpGet("config")]
    [AllowAnonymous]
    [ApiDescription("Query login configuration")]
    public async Task<ActionResult<ApiResponse<LoginConfigResult>>> ConfigAsync() {
        var sliderEnabled = await db
            .DictionaryItems.AnyAsync(x =>
                x.Category.Code == "system_settings" && x.Label == "Enable login slider verification" && x.Value == "true" && x.IsEnabled
            )
            .ConfigureAwait(false);
        var registrationEnabled = await db
            .DictionaryItems
            .AnyAsync(x => x.Category.Code == "system_settings" && x.Label == "Enable user registration" && x.Value == "true" && x.IsEnabled)
            .ConfigureAwait(false);
        var emailVerificationEnabled = await IsSettingEnabledAsync("Enable email verification").ConfigureAwait(false);
        return Ok(ApiResponse<LoginConfigResult>.Ok(new LoginConfigResult(sliderEnabled, registrationEnabled, emailVerificationEnabled)));
    }

    /// <summary>
    ///     校验账号密码并签发访问令牌
    /// </summary>
    /// <param name="request">登录凭据</param>
    /// <returns>登录结果或失败响应</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ApiDescription("Sign in to the system")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> LoginAsync(LoginRequest request) {
        if (string.IsNullOrWhiteSpace(await cache.GetStringAsync($"login-proof:{request.Challenge}").ConfigureAwait(false))
            || !IsValidProof(request.Challenge, request.Proof, _PROOF_DIFFICULTY)) {
            return Unauthorized(new ApiResponse<object>(401, "Login verification expired, please try again", null));
        }

        await cache.RemoveAsync($"login-proof:{request.Challenge}").ConfigureAwait(false);

        // 校验用户状态和密码，成功后返回带角色声明的令牌。
        var user = await db
            .Users.Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.UserName == request.UserName)
            .ConfigureAwait(false);
        return user?.IsEnabled != true || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)
            ? Unauthorized(new ApiResponse<object>(401, "Invalid username or password", null))
            : Ok(ApiResponse<LoginResult>.Ok(new LoginResult(tokenService.Create(user), string.Empty), "Login successful"));
    }

    /// <summary>
    ///     注册普通用户
    /// </summary>
    /// <param name="request">注册信息</param>
    /// <returns>注册结果</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ApiDescription("Register user")]
    public async Task<ActionResult<ApiResponse<object>>> RegisterAsync(RegisterRequest request) {
        var enabled = await IsSettingEnabledAsync("Enable user registration").ConfigureAwait(false);
        if (!enabled) {
            return BadRequest(new ApiResponse<object>(400, "User registration is disabled", null));
        }

        var codeValid = !await IsSettingEnabledAsync("Enable email verification").ConfigureAwait(false)
                        || await cache.GetStringAsync($"register-code:{request.Email.Trim().ToLowerInvariant()}").ConfigureAwait(false)
                        == request.VerificationCode.Trim();
        if (!codeValid) {
            return BadRequest(new ApiResponse<object>(400, "Invalid email verification code", null));
        }

        var userName = request.UserName.Trim();
        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.UserName == userName).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Username already exists", null));
        }

        if (await db.Users.AnyAsync(x => x.Email == email).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Email already exists", null));
        }

        var invitationCode = request.InvitationCode?.Trim().ToUpperInvariant();
        var inviter = string.IsNullOrWhiteSpace(invitationCode)
            ? null
            : await db.Users.SingleOrDefaultAsync(x => x.InvitationCode == invitationCode).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(invitationCode) && inviter is null) {
            return BadRequest(new ApiResponse<object>(400, "Invitation code is invalid", null));
        }

        var role = await db.Roles.SingleAsync(x => x.Code == "R_USER").ConfigureAwait(false);
        var defaultDepartment = await db.Departments.SingleAsync(x => x.Code == Department.DEFAULT_CODE).ConfigureAwait(false);
        Department? inviterDepartment = null;
        if (inviter is not null) {
            // 有邀请者时，新用户个人部门挂在邀请者个人部门下，确保部门数据权限覆盖多级邀请关系
            inviterDepartment = await db.Departments.SingleOrDefaultAsync(x => x.Code == $"USER_{inviter.Id}").ConfigureAwait(false);
            if (inviterDepartment is null) {
                return StatusCode(500, new ApiResponse<object>(500, "Inviter department does not exist", null));
            }
        }

        var user = new User { UserName = userName, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        user.UserRoles.Add(new UserRole { User = user, Role = role });

        var personalDepartment = new Department
        {
            Name = user.UserName, Code = $"USER_{user.Id}", ParentId = inviterDepartment?.Id ?? defaultDepartment.Id, Sort = 0
        };
        user.UserDepartments.Add(new UserDepartment { User = user, Department = personalDepartment });

        // 事务确保用户、邀请关系、个人部门及关联数据同时创建成功
        await using var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);
        _ = await db.Users.AddAsync(user).ConfigureAwait(false);
        if (inviterDepartment is not null) {
            // 受邀用户同时加入邀请人的个人部门，用于按邀请关系管理成员
            user.UserDepartments.Add(new UserDepartment { User = user, Department = inviterDepartment });
            _ = await db
                .UserReferrals.AddAsync(
                    new UserReferral { Invitee = user, InviteeUserId = user.Id, OwnerId = inviter!.Id, OwnerDepartmentId = inviterDepartment.Id }
                )
                .ConfigureAwait(false);
        }

        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Registration successful"));
    }

    /// <summary>
    ///     发送注册邮箱验证码
    /// </summary>
    /// <param name="request">邮箱请求</param>
    /// <returns>发送结果</returns>
    [HttpPost("register-code")]
    [AllowAnonymous]
    [ApiDescription("Send registration verification code")]
    public async Task<ActionResult<ApiResponse<object>>> RegisterCodeAsync(RegisterCodeRequest request) {
        if (!await IsSettingEnabledAsync("Enable email verification").ConfigureAwait(false)) {
            return Ok(ApiResponse<object>.Ok(new { }));
        }

        var ticketKey = $"register-puzzle-ticket:{request.PuzzleTicket}";
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var ticketEmail = await cache.GetStringAsync(ticketKey, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!string.Equals(ticketEmail, normalizedEmail, StringComparison.Ordinal)) {
            return BadRequest(new ApiResponse<object>(400, "Puzzle verification is required", null));
        }

        await cache.RemoveAsync(ticketKey, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(
                await cache.GetStringAsync($"register-code-cooldown:{normalizedEmail}", HttpContext.RequestAborted).ConfigureAwait(false)
            )) {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiResponse<object>(429, "Verification code was sent recently", null));
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var smtp = await GetSmtpSettingsAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From)) {
            return BadRequest(new ApiResponse<object>(400, "SMTP is not configured", null));
        }

        var sender = string.IsNullOrWhiteSpace(smtp.User) ? smtp.From : smtp.User;
        var mail = new MimeMessage { Date = DateTimeOffset.Now, MessageId = MimeUtils.GenerateMessageId() };
        mail.From.Add(MailboxAddress.Parse(sender));
        mail.To.Add(MailboxAddress.Parse(request.Email.Trim()));
        mail.Subject = "Registration verification code";
        mail.Body = new TextPart("plain") { Text = $"Your verification code is {code}. It expires in 10 minutes." };
        using var client = new SmtpClient();
        var socket = smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(smtp.Host, smtp.Port, socket, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(smtp.User)) {
            await client.AuthenticateAsync(smtp.User, smtp.Password, HttpContext.RequestAborted).ConfigureAwait(false);
        }

        var smtpResponse = await client.SendAsync(mail, HttpContext.RequestAborted).ConfigureAwait(false);
        await client.DisconnectAsync(true, HttpContext.RequestAborted).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information)) {
            _logSmtpAccepted(logger, mail.MessageId, request.Email.Trim(), smtpResponse, null);
        }

        await cache
            .SetStringAsync(
                $"register-code:{normalizedEmail}", code
                , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }
            )
            .ConfigureAwait(false);
        await cache
            .SetStringAsync(
                $"register-code-cooldown:{normalizedEmail}", "1"
                , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) }, HttpContext.RequestAborted
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    ///     创建发送邮箱验证码前的拼图挑战
    /// </summary>
    /// <returns>拼图挑战</returns>
    [HttpGet("register-puzzle")]
    [AllowAnonymous]
    [ApiDescription("Create registration email puzzle")]
    public async Task<ActionResult<ApiResponse<RegisterPuzzleResult>>> RegisterPuzzleAsync() {
        var challengeId = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var targetX = RandomNumberGenerator.GetInt32(90, _PUZZLE_WIDTH - _PUZZLE_PIECE_SIZE - 15);
        var targetY = RandomNumberGenerator.GetInt32(35, _PUZZLE_HEIGHT - _PUZZLE_PIECE_SIZE - 15);
        var (background, piece) = CreatePuzzleImages(targetX, targetY);
        await cache
            .SetStringAsync(
                $"register-puzzle:{challengeId}", targetX.ToString(CultureInfo.InvariantCulture)
                , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _challengeLifetime }, HttpContext.RequestAborted
            )
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<RegisterPuzzleResult>.Ok(
                new RegisterPuzzleResult(challengeId, background, piece, _PUZZLE_WIDTH, _PUZZLE_HEIGHT, _PUZZLE_PIECE_SIZE, targetY)
            )
        );
    }

    /// <summary>
    ///     校验发送邮箱验证码前的拼图位置
    /// </summary>
    /// <param name="request">拼图校验请求</param>
    /// <returns>一次性发送凭证</returns>
    [HttpPost("register-puzzle/verify")]
    [AllowAnonymous]
    [ApiDescription("Verify registration email puzzle")]
    public async Task<ActionResult<ApiResponse<VerifyRegisterPuzzleResult>>> VerifyRegisterPuzzleAsync(VerifyRegisterPuzzleRequest request) {
        var key = $"register-puzzle:{request.ChallengeId}";
        var expected = await cache.GetStringAsync(key, HttpContext.RequestAborted).ConfigureAwait(false);
        await cache.RemoveAsync(key, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!int.TryParse(expected, NumberStyles.None, CultureInfo.InvariantCulture, out var targetX) || Math.Abs(targetX - request.OffsetX) > 5) {
            return BadRequest(new ApiResponse<object>(400, "Puzzle verification failed", null));
        }

        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await cache
            .SetStringAsync(
                $"register-puzzle-ticket:{ticket}", request.Email.Trim().ToLowerInvariant()
                , new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _challengeLifetime }, HttpContext.RequestAborted
            )
            .ConfigureAwait(false);
        return Ok(ApiResponse<VerifyRegisterPuzzleResult>.Ok(new VerifyRegisterPuzzleResult(ticket)));
    }

    private static (string Background, string Piece) CreatePuzzleImages(
        int targetX
        , int targetY
    ) {
        var hue = RandomNumberGenerator.GetInt32(0, 360);
        var background = $"""
                          <svg xmlns="http://www.w3.org/2000/svg" width="{_PUZZLE_WIDTH}" height="{_PUZZLE_HEIGHT}">
                            <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                              <stop stop-color="hsl({hue} 68% 52%)"/><stop offset="1" stop-color="hsl({(hue + 75) % 360} 72% 38%)"/>
                            </linearGradient></defs>
                            <rect width="100%" height="100%" fill="url(#g)"/>
                            <circle cx="55" cy="42" r="31" fill="white" opacity=".16"/>
                            <circle cx="260" cy="118" r="48" fill="white" opacity=".12"/>
                            <path d="M0 125 Q80 85 160 125 T320 115 V160 H0Z" fill="white" opacity=".13"/>
                            <rect x="{targetX}" y="{targetY}" width="{_PUZZLE_PIECE_SIZE}" height="{_PUZZLE_PIECE_SIZE}" rx="8"
                                  fill="#111827" opacity=".48" stroke="white" stroke-width="2" stroke-dasharray="4 3"/>
                          </svg>
                          """;
        var piece = $"""
                     <svg xmlns="http://www.w3.org/2000/svg" width="{_PUZZLE_PIECE_SIZE}" height="{_PUZZLE_PIECE_SIZE}">
                       <rect x="1" y="1" width="42" height="42" rx="8" fill="white" fill-opacity=".9" stroke="#111827" stroke-width="2"/>
                       <path d="M13 22l6 6 13-14" fill="none" stroke="hsl({hue} 72% 38%)" stroke-width="4" stroke-linecap="round"/>
                     </svg>
                     """;
        return (ToSvgDataUrl(background), ToSvgDataUrl(piece));
    }

    private static bool IsValidProof(
        string challenge
        , string proof
        , int difficulty
    ) {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > 32) {
            return false;
        }

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge}:{proof}")));
        return digest.StartsWith(new string('0', difficulty), StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSvgDataUrl(string svg) {
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    private async Task<(string Host, int Port, bool EnableSsl, string User, string Password, string From)> GetSmtpSettingsAsync() {
        var values = await db
            .DictionaryItems.Where(x => x.Category.Code == "system_settings" && x.Label.StartsWith("SMTP "))
            .ToDictionaryAsync(x => x.Label, x => x.Value)
            .ConfigureAwait(false);
        return (values.GetValueOrDefault("SMTP Host", string.Empty)
            , int.TryParse(values.GetValueOrDefault("SMTP Port", "25"), out var port) ? port : 25
            , values.GetValueOrDefault("SMTP SSL", "true") == "true", values.GetValueOrDefault("SMTP User", string.Empty)
            , values.GetValueOrDefault("SMTP Password", string.Empty), values.GetValueOrDefault("SMTP From", string.Empty));
    }

    private Task<bool> IsSettingEnabledAsync(string label) {
        return db.DictionaryItems.AnyAsync(x => x.Category.Code == "system_settings" && x.Label == label && x.Value == "true" && x.IsEnabled);
    }
}