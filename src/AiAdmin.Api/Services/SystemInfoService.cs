using System.Reflection;
using System.Text.RegularExpressions;
using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Services;

/// <summary>
///     提供系统名称、版本号和最近一次版本更新内容，数据读取自程序集元数据与嵌入的 CHANGELOG
/// </summary>
public sealed partial class SystemInfoService
{
    private readonly Lazy<DashboardSystemInfoResult> _info = new(Build);

    /// <summary>
    ///     系统信息，进程生命周期内不会变化，首次访问时构建并缓存
    /// </summary>
    public DashboardSystemInfoResult Info => _info.Value;

    /// <summary>
    ///     从当前程序集构建系统信息
    /// </summary>
    /// <returns>系统名称、版本号与最近一次版本的更新内容</returns>
    private static DashboardSystemInfoResult Build() {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? assembly.GetName().Name ?? string.Empty;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? assembly.GetName().Version?.ToString() ?? string.Empty;
        var (releaseDate, notes) = ParseLatestRelease(assembly);

        // 去除构建元数据（如提交哈希）
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        var version = plus >= 0 ? informational[..plus] : informational;
        return new DashboardSystemInfoResult(name, version, releaseDate, notes);
    }

    /// <summary>
    ///     解析嵌入 CHANGELOG 中最近一次版本的发布日期和更新内容
    /// </summary>
    /// <param name="assembly">包含嵌入 CHANGELOG 的程序集</param>
    /// <returns>发布日期与更新内容分组</returns>
    private static (string? ReleaseDate, List<DashboardReleaseNoteSection> Notes) ParseLatestRelease(Assembly assembly) {
        var notes = new List<DashboardReleaseNoteSection>();
        using var stream = assembly.GetManifestResourceStream("CHANGELOG.md");
        if (stream is null) {
            return (null, notes);
        }

        using var reader = new StreamReader(stream);

        // 跳过第一个版本标题之前的内容，取到下一个版本标题为止
        string? headingLine = null;
        while (reader.ReadLine() is { } head) {
            if (_releaseHeading().IsMatch(head)) {
                headingLine = head;
                break;
            }
        }

        if (headingLine is null) {
            return (null, notes);
        }

        var dateMatch = _releaseDate().Match(headingLine);
        var releaseDate = dateMatch.Success ? dateMatch.Groups[1].Value : null;
        List<string>? items = null;
        while (reader.ReadLine() is { } line && !_releaseHeading().IsMatch(line)) {
            items = ReadReleaseLine(line, items, notes);
        }

        return (releaseDate, notes.FindAll(x => x.Items.Count > 0));
    }

    /// <summary>
    ///     处理版本标题之后的一行内容：小节标题开启新分组，条目行追加到当前分组
    /// </summary>
    /// <param name="line">CHANGELOG 中的一行</param>
    /// <param name="items">当前分组的条目集合，尚未遇到小节标题时为空</param>
    /// <param name="notes">已解析的更新内容分组</param>
    /// <returns>处理后的当前分组条目集合</returns>
    private static List<string>? ReadReleaseLine(
        string line
        , List<string>? items
        , List<DashboardReleaseNoteSection> notes
    ) {
        if (line.StartsWith("### ", StringComparison.Ordinal)) {
            var section = new List<string>();
            notes.Add(new DashboardReleaseNoteSection(line[4..].Trim(), section));
            return section;
        }

        // 条目行以星号开头，换行产生的缩进续行（提交链接）直接忽略
        if (items is null || !line.StartsWith("* ", StringComparison.Ordinal)) {
            return items;
        }

        var text = _commitLink().Replace(line[2..], string.Empty).Replace("**", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length > 0) {
            items.Add(text);
        }

        return items;
    }

    /// <summary>
    ///     匹配条目末尾的提交链接
    /// </summary>
    /// <returns>提交链接正则</returns>
    [GeneratedRegex(@"\s*\(\[[0-9a-f]+\]\([^)]*\)\)\s*$")]
    private static partial Regex _commitLink();

    /// <summary>
    ///     匹配版本标题行末尾的发布日期
    /// </summary>
    /// <returns>发布日期正则</returns>
    [GeneratedRegex(@"\((\d{4}-\d{2}-\d{2})\)\s*$")]
    private static partial Regex _releaseDate();

    /// <summary>
    ///     匹配 CHANGELOG 中的版本标题行
    /// </summary>
    /// <returns>版本标题正则</returns>
    [GeneratedRegex(@"^#{2,3} \[")]
    private static partial Regex _releaseHeading();
}