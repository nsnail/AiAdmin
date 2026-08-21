// 定义需要受数据权限控制的实体所有者信息
namespace AiAdmin.Api.Models;

/// <summary>
///     定义业务数据的所有者和所属部门
/// </summary>
public interface IOwner
{
    /// <summary>
    ///     所有者部门主键
    /// </summary>
    long OwnerDepartmentId { get; set; }

    /// <summary>
    ///     所有者用户主键
    /// </summary>
    long OwnerId { get; set; }
}