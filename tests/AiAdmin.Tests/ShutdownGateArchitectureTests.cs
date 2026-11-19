using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     架构守卫：扫描 src 下的源码，强制所有脱离请求生命周期运行的后台工作都登记到安全停机闸门。
///     新增了未登记的后台工作时本测试会失败，提示开发者改用 ShutdownGate.Track / Enter，或在白名单中写明豁免理由
/// </summary>
public sealed partial class ShutdownGateArchitectureTests
{
    /// <summary>
    ///     允许出现裸 fire-and-forget 的位置：文件名后缀 -> 豁免理由。只有 Track 自身可以这样写
    /// </summary>
    private static readonly Dictionary<string, string> _fireAndForgetAllowList = new(StringComparer.Ordinal)
    {
        ["AiAdmin.Core/Services/ShutdownGate.cs"] = "Track 的实现本身：先同步登记租约，再启动被登记的任务"
    };

    /// <summary>
    ///     禁止出现的“不可登记”的并发原语：它们会脱离 Track 的控制，使用前必须改走 ShutdownGate.Track
    /// </summary>
    private static readonly string[] _forbiddenPatterns =
    [
        "Task.Run(", "Task.Factory.StartNew(", "new Thread(", "ThreadPool.QueueUserWorkItem(", "ThreadPool.UnsafeQueueUserWorkItem(", "new Timer("
        , "new System.Threading.Timer(", "PeriodicTimer", ".ContinueWith("
    ];

    /// <summary>
    ///     后台服务（BackgroundService / IHostedService）白名单：类名 -> 登记方式说明。新增后台服务必须在此登记并说明其工作如何计入在途
    /// </summary>
    private static readonly Dictionary<string, string> _hostedServices = new(StringComparer.Ordinal)
    {
        ["ScheduledJobHostedService"] = "每轮扫描 Enter(Job)，作业执行经 Track(Job) 发起，关闸后不再触发新作业"
        , ["ScheduledJobReleaseHostedService"] = "每轮扫描 Enter(Job)，关闸后跳过本轮"
        , ["ElasticsearchLogBackgroundService"] = "仅在取到一批日志后 Enter(Background)，批次写完释放；空队列轮询不计入；关闸后经 WaitWhileClosedAsync 暂停拉取，积压留在共享队列"
        , ["ApiLogStorageBackgroundService"] = "仅在取到一批请求日志后 Enter(Background)，批次写入分表后释放；写入失败时放回队列，空队列轮询不计入；关闸后经 WaitWhileClosedAsync 暂停拉取"
        , ["RealtimeShutdownHostedService"] = "只在启动时订阅总线、停止时关闸，不含持续工作"
    };

    /// <summary>
    ///     白名单中每一项都不能没有说明
    /// </summary>
    [Fact]
    public void AllowListEntriesHaveReasons() {
        Assert.All(_fireAndForgetAllowList.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        Assert.All(_hostedServices.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    /// <summary>
    ///     所有后台服务都必须在白名单中登记，且引用了 ShutdownGate（RealtimeShutdownHostedService 这类只负责关闸的除外也需登记说明）
    /// </summary>
    [Fact]
    public void EveryHostedServiceIsDeclaredAndUsesShutdownGate() {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text) in ReadSources()) {
            foreach (Match match in HostedServiceClass().Matches(text)) {
                found[match.Groups["name"].Value] = text;
            }

            _ = path;
        }

        var undeclared = found.Keys.Where(x => !_hostedServices.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(
            undeclared.Count == 0
            , "Hosted services must be declared in the allow list with an explanation of how their work is tracked: " + string.Join(", ", undeclared)
        );

        var stale = _hostedServices.Keys.Where(x => !found.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0, "Allow list contains hosted services that no longer exist: " + string.Join(", ", stale));

        var withoutGate = found
            .Where(x => !x.Value.Contains("ShutdownGate", StringComparison.Ordinal))
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.True(withoutGate.Count == 0, "Hosted service source files must reference ShutdownGate: " + string.Join(", ", withoutGate));
    }

    /// <summary>
    ///     源码中不得出现裸 fire-and-forget，必须通过 ShutdownGate.Track 发起
    /// </summary>
    [Fact]
    public void NoBareFireAndForgetOutsideTrack() {
        var violations = new List<string>();
        foreach (var (path, text) in ReadSources()) {
            if (_fireAndForgetAllowList.Keys.Any(allowed => path.EndsWith(allowed, StringComparison.Ordinal))) {
                continue;
            }

            foreach (Match match in BareFireAndForget().Matches(text)) {
                violations.Add($"{path}:{LineOf(text, match.Index)}: {match.Value.Trim()}");
            }
        }

        Assert.True(
            violations.Count == 0, "Bare fire-and-forget must use ShutdownGate.Track so safe shutdown can see it:\n" + string.Join("\n", violations)
        );
    }

    /// <summary>
    ///     源码中不得使用会脱离闸门控制的并发原语
    /// </summary>
    [Fact]
    public void NoUntrackableConcurrencyPrimitives() {
        var violations = new List<string>();
        foreach (var (path, text) in ReadSources()) {
            violations.AddRange(
                from pattern in _forbiddenPatterns
                let index = text.IndexOf(pattern, StringComparison.Ordinal)
                where index >= 0
                select $"{path}:{LineOf(text, index)}: {pattern}"
            );
        }

        Assert.True(
            violations.Count == 0
            , "These primitives escape ShutdownGate tracking; run the work through ShutdownGate.Track instead:\n" + string.Join("\n", violations)
        );
    }

    /// <summary>
    ///     裸 fire-and-forget：以 “_ = ” 开头、调用以 Async 结尾的方法、且没有 await
    /// </summary>
    /// <returns>匹配器</returns>
    [GeneratedRegex(@"^\s*_ = (?!await\b)[A-Za-z_][\w\.<>]*Async\(", RegexOptions.Multiline | RegexOptions.CultureInvariant, 2000)]
    private static partial Regex BareFireAndForget();

    /// <summary>
    ///     由本源文件在编译期的真实路径推算仓库根目录（tests/AiAdmin.Tests/ 的上两级），不依赖构建输出目录
    /// </summary>
    /// <param name="sourceFile">调用方源文件路径，由编译器填充</param>
    /// <returns>仓库根目录</returns>
    /// <exception cref="DirectoryNotFoundException">推算出的目录下没有 src 时引发</exception>
    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "") {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
        return Directory.Exists(Path.Combine(root, "src"))
            ? root
            : throw new DirectoryNotFoundException("Repository root not found from " + sourceFile);
    }

    /// <summary>
    ///     继承 BackgroundService 或实现 IHostedService / IHostedLifecycleService 的类
    /// </summary>
    /// <returns>匹配器</returns>
    [GeneratedRegex(
        @"class\s+(?<name>\w+)\b[^{;]*?:\s*[^{;]*\b(BackgroundService|IHostedService|IHostedLifecycleService)\b"
        , RegexOptions.Singleline | RegexOptions.CultureInvariant, 2000
    )]
    private static partial Regex HostedServiceClass();

    /// <summary>
    ///     计算字符位置所在的行号
    /// </summary>
    /// <param name="text">源码文本</param>
    /// <param name="index">字符位置</param>
    /// <returns>从 1 开始的行号</returns>
    private static int LineOf(
        string text
        , int index
    ) {
        return text.AsSpan(0, index).Count('\n') + 1;
    }

    /// <summary>
    ///     读取 src 下所有生产源码，排除 obj、bin 和前端目录
    /// </summary>
    /// <returns>相对路径与源码文本</returns>
    private static IEnumerable<(string Path, string Text)> ReadSources() {
        var root = FindRepositoryRoot();
        var src = Path.Combine(root, "src");
        return Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !x.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !x.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            )
            .Select(x => (Path.GetRelativePath(root, x).Replace('\\', '/'), File.ReadAllText(x)));
    }
}