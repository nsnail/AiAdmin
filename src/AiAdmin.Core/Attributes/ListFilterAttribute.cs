// 标记实体字段在列表筛选栏中的控件呈现方式
namespace AiAdmin.Api.Attributes;

/// <summary>
///     定义实体属性可用于列表筛选的前端控件元数据
/// </summary>
/// <param name="label">字段显示名称的客户端多语言键</param>
/// <param name="control">前端控件类型</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ListFilterAttribute(string label, string control = "input") : Attribute
{
    /// <summary>
    ///     前端控件类型
    /// </summary>
    public string Control { get; } = control;

    /// <summary>
    ///     字段显示名称的客户端多语言键
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    ///     下拉或单选选项，格式为值:客户端多语言键
    /// </summary>
    public string[] Options { get; init; } = [];

    /// <summary>
    ///     输入提示文字的客户端多语言键
    /// </summary>
    public string Placeholder { get; init; } = string.Empty;

    /// <summary>
    ///     控件在筛选栏中的显示顺序，数值越小越靠前
    /// </summary>
    public int Sort { get; init; } = int.MaxValue;

    /// <summary>
    ///     控件占用的二十四栅格列数
    /// </summary>
    public int Span { get; init; } = 6;
}