namespace AiAdmin.Api.Contracts;

/// <summary>
///     邀请关系树节点
/// </summary>
public sealed class ReferralTreeItem
{
    /// <summary>
    ///     直接下级集合
    /// </summary>
    public IReadOnlyList<ReferralTreeItem> Children { get; init; } = [];

    /// <summary>
    ///     注册时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     电子邮箱
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    ///     用户主键
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    ///     该用户继续邀请其他用户时使用的邀请码
    /// </summary>
    public required string InvitationCode { get; init; }

    /// <summary>
    ///     登录用户名
    /// </summary>
    public required string UserName { get; init; }
}