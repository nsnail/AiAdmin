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
        string? releaseDate = null;
        var started = false;
        List<string>? items = null;
        while (reader.ReadLine() is { } line) {
            if (_releaseHeading().IsMatch(line)) {
                if (started) {
                    break;
                }

                started = true;
                var dateMatch = _releaseDate().Match(line);
                releaseDate = dateMatch.Success ? dateMatch.Groups[1].Value : null;
                continue;
            }

            if (!started) {
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal)) {
                items = [];
                notes.Add(new DashboardReleaseNoteSection(line[4..].Trim(), items));
                continue;
            }

            // 条目行以星号开头，换行产生的缩进续行（提交链接）直接忽略
            if (items is not null && line.StartsWith("* ", StringComparison.Ordinal)) {
                var text = _commitLink().Replace(line[2..], string.Empty).Replace("**", string.Empty, StringComparison.Ordinal).Trim();
                if (text.Length > 0) {
                    items.Add(text);
                }
            }
        }

        return (releaseDate, notes.FindAll(x => x.Items.Count > 0));
    }
}