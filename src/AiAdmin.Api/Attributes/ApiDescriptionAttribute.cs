// 标注控制器或操作的接口描述，供接口同步和多语言展示使用。

namespace AiAdmin.Api.Attributes;

/// <summary>
///     标记控制器或操作的接口描述键
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class ApiDescriptionAttribute(string description) : Attribute
{
    /// <summary>
    ///     获取英文接口描述
    /// </summary>
    // 保存接口的英文描述键，中文由多语言服务按请求语言转换。
    public string Description { get; } = description;
}