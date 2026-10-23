namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘系统信息结果，名称和版本号来自后端程序集元数据，更新内容来自嵌入的 CHANGELOG
/// </summary>
/// <param name="Name">系统名称</param>
/// <param name="Version">系统版本号</param>
/// <param name="ReleaseDate">最近一次版本发布日期，无法解析时为空</param>
/// <param name="ReleaseNotes">最近一次版本的更新内容分组</param>
public sealed record DashboardSystemInfoResult(
    string Name
    , string Version
    , string? ReleaseDate
    , IReadOnlyList<DashboardReleaseNoteSection> ReleaseNotes);