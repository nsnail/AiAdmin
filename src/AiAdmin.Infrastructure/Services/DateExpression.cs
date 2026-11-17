using System.Globalization;

namespace AiAdmin.Api.Services;

/// <summary>
///     动态筛选日期值的 C# 表达式解析器，例如 DateTime.Now.Date 表示今日零点；
///     只解析白名单内的 DateTime 成员链，不编译也不执行任意代码，因此可以安全地接收请求体中的文本
/// </summary>
public static class DateExpression
{
    /// <summary>
    ///     表达式最大长度，超过时不按表达式处理
    /// </summary>
    private const int _MAX_LENGTH = 128;

    /// <summary>
    ///     允许的类型前缀，较长的放在前面
    /// </summary>
    private static readonly string[] _typePrefixes = ["DateTimeOffset.", "DateTime."];

    /// <summary>
    ///     把文本解析为日期，优先按表达式处理，其次按 ISO 日期格式处理
    /// </summary>
    /// <param name="text">日期文本或日期表达式</param>
    /// <returns>解析后的日期</returns>
    /// <exception cref="DynamicFilterValidationException">表达式无效时抛出</exception>
    /// <exception cref="FormatException">既不是表达式也不是有效日期时抛出</exception>
    public static DateTimeOffset ParseDate(string text) {
        return TryResolve(text, DateTimeOffset.Now, out var result)
            ? result
            : DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    /// <summary>
    ///     尝试把文本解析为日期，优先按表达式处理，其次按 ISO 日期格式处理
    /// </summary>
    /// <param name="text">日期文本或日期表达式</param>
    /// <param name="result">解析结果</param>
    /// <returns>解析成功返回 true</returns>
    /// <exception cref="DynamicFilterValidationException">文本是表达式但内容无效时抛出</exception>
    public static bool TryParseDate(
        string text
        , out DateTimeOffset result
    ) {
        return TryResolve(text, DateTimeOffset.Now, out result)
               || DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }

    /// <summary>
    ///     尝试按日期表达式求值；文本不是以 DateTime 开头的表达式时返回 false，由调用方按普通日期处理
    /// </summary>
    /// <param name="text">日期表达式，例如 DateTime.Now.Date.AddDays(-1)</param>
    /// <param name="now">当前时间，带本地时区偏移，Now 与 Today 以它为准</param>
    /// <param name="result">求值结果</param>
    /// <returns>文本是有效表达式并求值成功返回 true，不是表达式返回 false</returns>
    /// <exception cref="DynamicFilterValidationException">文本是表达式但包含不支持的成员或参数无效时抛出</exception>
    public static bool TryResolve(
        string text
        , DateTimeOffset now
        , out DateTimeOffset result
    ) {
        result = default;
        var body = StripRoot(text);
        if (body is null) {
            return false;
        }

        try {
            result = Evaluate(ReadSegments(body), now);
            return true;
        }
        catch (ArgumentException) {
            // AddDays 等运算超出 DateTimeOffset 可表示范围
            throw CreateInvalidException();
        }
    }

    /// <summary>
    ///     应用 Date 属性或 AddXxx 方法
    /// </summary>
    /// <param name="value">当前值</param>
    /// <param name="segment">成员名称与参数</param>
    /// <returns>应用后的值</returns>
    /// <exception cref="DynamicFilterValidationException">成员不受支持或参数无效时抛出</exception>
    private static DateTimeOffset ApplyMember(
        DateTimeOffset value
        , (string Name, string? Argument) segment
    ) {
        if (segment.Argument is null) {
            return segment.Name == "Date" ? ToMidnight(value) : throw CreateInvalidException();
        }

        return segment.Name switch
        {
            "AddYears" => value.AddYears(ReadInt(segment.Argument))
            , "AddMonths" => value.AddMonths(ReadInt(segment.Argument))
            , "AddDays" => value.AddDays(ReadDouble(segment.Argument))
            , "AddHours" => value.AddHours(ReadDouble(segment.Argument))
            , "AddMinutes" => value.AddMinutes(ReadDouble(segment.Argument))
            , "AddSeconds" => value.AddSeconds(ReadDouble(segment.Argument))
            , _ => throw CreateInvalidException()
        };
    }

    /// <summary>
    ///     应用第一段成员，只允许 Now、UtcNow 和 Today
    /// </summary>
    /// <param name="segment">第一段成员</param>
    /// <param name="now">当前时间</param>
    /// <returns>起始时间</returns>
    /// <exception cref="DynamicFilterValidationException">成员不受支持或带有参数时抛出</exception>
    private static DateTimeOffset ApplyRoot(
        (string Name, string? Argument) segment
        , DateTimeOffset now
    ) {
        return segment.Argument is null
            ? segment.Name switch
            {
                "Now" => now
                , "UtcNow" => now.ToUniversalTime()
                , "Today" => ToMidnight(now)
                , _ => throw CreateInvalidException()
            }
            : throw CreateInvalidException();
    }

    /// <summary>
    ///     校验成员分隔符并移动到下一段，成员之间必须以点号连接且不能以点号结尾
    /// </summary>
    /// <param name="body">表达式主体</param>
    /// <param name="position">当前位置</param>
    /// <exception cref="DynamicFilterValidationException">分隔符无效时抛出</exception>
    private static void ConsumeSeparator(
        string body
        , ref int position
    ) {
        if (position >= body.Length) {
            return;
        }

        ++position;
        if (body[position - 1] != '.' || position == body.Length) {
            throw CreateInvalidException();
        }
    }

    /// <summary>
    ///     创建表达式无效异常
    /// </summary>
    /// <returns>动态筛选格式异常</returns>
    private static DynamicFilterValidationException CreateInvalidException() {
        return new DynamicFilterValidationException("Dynamic filter date expression is invalid.");
    }

    /// <summary>
    ///     对第一段之后的成员链依次求值
    /// </summary>
    /// <param name="segments">成员链，第一段是 Now、UtcNow 或 Today</param>
    /// <param name="now">当前时间</param>
    /// <returns>求值结果</returns>
    private static DateTimeOffset Evaluate(
        List<(string Name, string? Argument)> segments
        , DateTimeOffset now
    ) {
        var value = ApplyRoot(segments[0], now);

        return segments.Skip(1).Aggregate(value, ApplyMember);
    }

    /// <summary>
    ///     读取浮点参数，用于 AddDays 等方法
    /// </summary>
    /// <param name="argument">参数文本</param>
    /// <returns>参数值</returns>
    /// <exception cref="DynamicFilterValidationException">参数不是有效数字时抛出</exception>
    private static double ReadDouble(string argument) {
        return double.TryParse(argument, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw CreateInvalidException();
    }

    /// <summary>
    ///     读取整数参数，用于 AddYears 与 AddMonths
    /// </summary>
    /// <param name="argument">参数文本</param>
    /// <returns>参数值</returns>
    /// <exception cref="DynamicFilterValidationException">参数不是有效整数时抛出</exception>
    private static int ReadInt(string argument) {
        return int.TryParse(argument, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw CreateInvalidException();
    }

    /// <summary>
    ///     读取一个成员，格式为名称或名称加括号参数
    /// </summary>
    /// <param name="body">表达式主体</param>
    /// <param name="position">当前位置，读取后移动到成员末尾</param>
    /// <returns>成员名称与参数</returns>
    /// <exception cref="DynamicFilterValidationException">成员格式无效时抛出</exception>
    private static (string Name, string? Argument) ReadSegment(
        string body
        , ref int position
    ) {
        var nameEnd = body.IndexOfAny(['.', '('], position);
        if (nameEnd < 0) {
            nameEnd = body.Length;
        }

        var name = body[position..nameEnd];
        if (name.Length == 0 || !name.All(char.IsAsciiLetter)) {
            throw CreateInvalidException();
        }

        position = nameEnd;
        if (position >= body.Length || body[position] != '(') {
            return (name, null);
        }

        var close = body.IndexOf(')', position);
        if (close < 0) {
            throw CreateInvalidException();
        }

        var argument = body[(position + 1)..close];
        position = close + 1;
        return (name, argument);
    }

    /// <summary>
    ///     把表达式主体拆分为成员链
    /// </summary>
    /// <param name="body">去掉 DateTime 前缀后的表达式主体</param>
    /// <returns>成员链</returns>
    /// <exception cref="DynamicFilterValidationException">成员格式无效时抛出</exception>
    private static List<(string Name, string? Argument)> ReadSegments(string body) {
        var segments = new List<(string Name, string? Argument)>();
        var position = 0;
        while (position < body.Length) {
            segments.Add(ReadSegment(body, ref position));
            ConsumeSeparator(body, ref position);
        }

        return segments;
    }

    /// <summary>
    ///     去掉空白与 System 命名空间、DateTime 或 DateTimeOffset 前缀，得到成员链文本
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <returns>成员链文本，文本不是以 DateTime 开头的表达式或超长时返回空</returns>
    private static string? StripRoot(string text) {
        if (text.Length > _MAX_LENGTH) {
            return null;
        }

        var compact = string.Concat(text.Where(x => !char.IsWhiteSpace(x)));
        compact = compact.StartsWith("System.", StringComparison.Ordinal) ? compact["System.".Length..] : compact;
        var matched = _typePrefixes.FirstOrDefault(x => compact.StartsWith(x, StringComparison.Ordinal));
        return matched is null ? null : compact[matched.Length..];
    }

    /// <summary>
    ///     取同一时区偏移下当天的零点
    /// </summary>
    /// <param name="value">日期时间</param>
    /// <returns>当天零点</returns>
    private static DateTimeOffset ToMidnight(DateTimeOffset value) {
        return new DateTimeOffset(value.Date, value.Offset);
    }
}