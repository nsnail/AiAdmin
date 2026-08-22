namespace AiAdmin.Api.Models;

/// <summary>
///     定义支持乐观并发控制的实体
/// </summary>
public interface IVersion
{
    /// <summary>
    ///     实体版本号
    /// </summary>
    int Version { get; set; }
}