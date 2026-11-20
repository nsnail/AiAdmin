namespace AiAdmin.Api.Services;

/// <summary>
///     通用比较的结果：要么是命中或未命中，要么因值或阈值无法处理而失败并带原因
/// </summary>
/// <param name="Hit">条件是否命中，失败时为 true，让调用方按“取不到有效值”处理并报警</param>
/// <param name="Failed">值或阈值无法按比较类型处理</param>
/// <param name="Reason">失败原因，未失败时为空</param>
public sealed record MatchResult(bool Hit, bool Failed, string Reason)
{
    /// <summary>
    ///     创建失败的比较结果，调用方应视为取不到有效值
    /// </summary>
    /// <param name="reason">失败原因</param>
    /// <returns>比较结果</returns>
    public static MatchResult Invalid(string reason) {
        return new MatchResult(true, true, reason);
    }

    /// <summary>
    ///     创建正常的比较结果
    /// </summary>
    /// <param name="hit">条件是否命中</param>
    /// <returns>比较结果</returns>
    public static MatchResult Ok(bool hit) {
        return new MatchResult(hit, false, string.Empty);
    }
}