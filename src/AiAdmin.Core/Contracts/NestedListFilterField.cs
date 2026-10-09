namespace AiAdmin.Api.Contracts;

/// <summary>
///     声明导航属性上的列表筛选字段
/// </summary>
/// <param name="Field">字段路径</param>
/// <param name="Label">字段显示名称</param>
/// <param name="Control">前端控件类型</param>
/// <param name="Span">控件占用的栅格列数</param>
/// <param name="Sort">显示顺序</param>
/// <param name="Placeholder">输入提示文字</param>
/// <param name="ValueType">字段值类型</param>
/// <param name="GroupCount">是否启用分组计数</param>
/// <param name="IsVisible">是否显示在基础筛选栏</param>
public sealed record NestedListFilterField(
    string Field
    , string Label
    , string Control = "input"
    , int Span = 6
    , int Sort = int.MaxValue
    , string Placeholder = ""
    , string ValueType = "string"
    , bool GroupCount = false
    , bool IsVisible = true);