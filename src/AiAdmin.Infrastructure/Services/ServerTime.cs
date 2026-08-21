// 提供服务器本地时间读写和 API 时间响应转换能力。
namespace AiAdmin.Api.Services;

/// <summary>
///     服务器时间辅助类
/// </summary>
public static class ServerTime
{
    /// <summary>
    ///     获取服务器所在时区的当前本地时间
    /// </summary>
    public static DateTime Now => DateTime.Now;

    /// <summary>
    ///     将客户端带偏移量的时间转换为服务器本地时间
    /// </summary>
    /// <param name="value">客户端时间</param>
    /// <returns>服务器本地时间</returns>
    public static DateTime ToLocal(DateTimeOffset value) {
        return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Local).DateTime;
    }

    /// <summary>
    ///     将数据库中的服务器本地时间转换为带服务器偏移量的时间
    /// </summary>
    /// <param name="value">服务器本地时间</param>
    /// <returns>带服务器时区偏移量的时间</returns>
    public static DateTimeOffset ToOffset(DateTime value) {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}