namespace AiAdmin.Api.Models;

/// <summary>
///     标记所有者表示分配关系的业务数据，新增时不自动补当前用户为所有者
/// </summary>
public interface IAssignedOwner : IOwner;