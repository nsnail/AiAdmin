namespace AiAdmin.Api.Contracts;

/// <summary>
///     当前用户邀请关系查询结果
/// </summary>
public sealed class ReferralTreeResult
{
    /// <summary>
    ///     当前用户的直接下级及其后代
    /// </summary>
    public IReadOnlyList<ReferralTreeItem> Children { get; init; } = [];

    /// <summary>
    ///     当前用户的邀请码
    /// </summary>
    public required string InvitationCode { get; init; }
}