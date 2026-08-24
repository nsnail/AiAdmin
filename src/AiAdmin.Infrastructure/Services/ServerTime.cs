namespace AiAdmin.Api.Services;

/// <summary>
///     UTC 时间辅助类
/// </summary>
public static class ServerTime
{
    /// <summary>
    ///     获取当前 UTC 时间
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    ///     将数据库中的 UTC 时间转换为零偏移时间
    /// </summary>
    /// <param name="value">UTC 时间</param>
    /// <returns>零偏移时间</returns>
    public static DateTimeOffset ToOffset(DateTime value) {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }

    /// <summary>
    ///     将带偏移量的时间转换为 UTC 时间
    /// </summary>
    /// <param name="value">客户端时间</param>
    /// <returns>UTC 时间</returns>
    public static DateTime ToUtc(DateTimeOffset value) {
        return value.UtcDateTime;
    }
}