using System.IdentityModel.Tokens.Jwt;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证访问令牌与续签令牌的签发、校验、轮换，以及续签令牌不能当访问令牌使用、会话有绝对上限
/// </summary>
public sealed class TokenRenewalTests
{
    /// <summary>
    ///     验证登录签发两个令牌，续签令牌通过校验且携带会话和身份信息
    /// </summary>
    [Fact]
    public void Create_IssuesVerifiableRefreshToken() {
        var service = CreateService();
        var user = CreateUser();
        var (token, refreshToken, signKey) = service.Create(user);
        Assert.NotEqual(token, refreshToken);
        var ticket = service.ReadRefreshToken(refreshToken);
        Assert.NotNull(ticket);
        Assert.Equal(user.Id, ticket.UserId);
        Assert.Equal(service.CreateSigningKey(ticket.SessionId), signKey);
    }

    /// <summary>
    ///     验证访问令牌、篡改的令牌和乱码都不能作为续签令牌
    /// </summary>
    [Fact]
    public void ReadRefreshToken_RejectsAccessTokenAndGarbage() {
        var service = CreateService();
        var (token, refreshToken, _) = service.Create(CreateUser());
        Assert.Null(service.ReadRefreshToken(token));
        Assert.Null(service.ReadRefreshToken(refreshToken + "x"));
        Assert.Null(service.ReadRefreshToken("garbage"));
        Assert.Null(service.ReadRefreshToken(string.Empty));
    }

    /// <summary>
    ///     验证轮换沿用会话标识（签名密钥不变），并签发新的续签令牌
    /// </summary>
    [Fact]
    public void Rotate_KeepsSessionAndIssuesNewPair() {
        var service = CreateService();
        var (_, refreshToken, _) = service.Create(CreateUser());
        var ticket = service.ReadRefreshToken(refreshToken)!;
        var rotated = service.Rotate(ticket, "alice", ["R_USER"], DateTimeOffset.UtcNow);
        _ = Assert.NotNull(rotated);
        var next = service.ReadRefreshToken(rotated.Value.RefreshToken);
        Assert.NotNull(next);
        Assert.Equal(ticket.SessionId, next.SessionId);
        Assert.NotEqual(ticket.TokenId, next.TokenId);
        Assert.Equal(ticket.SessionStart, next.SessionStart);
    }

    /// <summary>
    ///     验证会话超过最长时长后不再续签，且新令牌的到期时间不会超过会话上限
    /// </summary>
    [Fact]
    public void Rotate_StopsAtMaximumSessionLength() {
        var service = CreateService();
        var (_, refreshToken, _) = service.Create(CreateUser());
        var ticket = service.ReadRefreshToken(refreshToken)!;
        Assert.Null(service.Rotate(ticket, "alice", ["R_USER"], ticket.SessionStart.AddMinutes(1000)));

        var nearEnd = service.Rotate(ticket, "alice", ["R_USER"], ticket.SessionStart.AddMinutes(950));
        _ = Assert.NotNull(nearEnd);
        var sessionEnd = ticket.SessionStart.AddMinutes(1000);
        var access = new JwtSecurityTokenHandler().ReadJwtToken(nearEnd.Value.Token);
        Assert.True(access.ValidTo <= sessionEnd.UtcDateTime.AddSeconds(1));
        Assert.True(service.ReadRefreshToken(nearEnd.Value.RefreshToken)!.ExpiresAt <= sessionEnd.AddSeconds(1));
    }

    /// <summary>
    ///     创建使用短时效配置的令牌服务
    /// </summary>
    /// <returns>令牌服务</returns>
    private static TokenService CreateService() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "unit-test-jwt-key-unit-test-jwt-key-123456"
                    , ["Jwt:Issuer"] = "issuer"
                    , ["Jwt:Audience"] = "audience"
                    , ["Jwt:ExpiresMinutes"] = "120"
                    , ["Jwt:RefreshExpiresMinutes"] = "600"
                    , ["Jwt:MaxSessionMinutes"] = "1000"
                }
            )
            .Build();
        return new TokenService(configuration);
    }

    /// <summary>
    ///     创建带一个启用角色的测试用户
    /// </summary>
    /// <returns>测试用户</returns>
    private static User CreateUser() {
        var role = new Role { Code = "R_USER", Name = "User", IsEnabled = true };
        var user = new User { UserName = "alice", PasswordHash = "hash" };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        return user;
    }
}