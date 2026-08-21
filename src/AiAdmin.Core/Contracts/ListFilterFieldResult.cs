namespace AiAdmin.Api.Contracts;

/// <summary>
///     列表筛选字段响应
/// </summary>
/// <param name="Field">实体属性名称</param>
/// <param name="Label">字段显示名称</param>
/// <param name="Control">前端控件类型</param>
/// <param name="Span">控件占用的二十四栅格列数</param>
/// <param name="Sort">控件显示顺序</param>
/// <param name="Placeholder">输入提示文字</param>
/// <param name="Options">可选项列表</param>
/// <param name="ValueType">字段值类型</param>
public sealed record ListFilterFieldResult(
    string Field
    , string Label
    , string Control
    , int Span
    , int Sort
    , string Placeholder
    , IReadOnlyList<ListFilterOptionResult> Options
    , string ValueType);