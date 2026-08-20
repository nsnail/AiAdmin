// 定义用户邀请关系及其数据权限归属

using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户邀请关系实体
/// </summary>
public sealed class UserReferral : EntityBase, IOwner
{
    /// <summary>
    ///     邀请关系主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     被邀请用户
    /// </summary>
    public User Invitee { get; init; } = null!;

    /// <summary>
    ///     被邀请用户主键
    /// </summary>
    public long InviteeUserId { get; init; }

    /// <summary>
    ///     邀请者个人部门主键
    /// </summary>
    public long OwnerDepartmentId { get; set; }

    /// <summary>
    ///     邀请者用户主键
    /// </summary>
    public long OwnerId { get; set; }
}