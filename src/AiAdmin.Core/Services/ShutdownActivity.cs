namespace AiAdmin.Api.Services;

/// <summary>
///     安全停机需要等待归零的在途工作类别
/// </summary>
public enum ShutdownActivity
{
    /// <summary>
    ///     正在处理的入站 HTTP 请求，含 WebSocket 连接
    /// </summary>
    Http = 0

    ,

    /// <summary>
    ///     正在执行的定时作业，以及作业调度和超时释放的单轮扫描
    /// </summary>
    Job = 1

    ,

    /// <summary>
    ///     正在进行的出站 HTTP 请求
    /// </summary>
    Outbound = 2

    ,

    /// <summary>
    ///     应用内的其他后台工作：日志入队与批量写入、缓存失效、实时推送分发等
    /// </summary>
    Background = 3

    ,

    /// <summary>
    ///     已打开尚未关闭的数据库连接，覆盖命令执行与事务期间；由 EF Core 连接拦截器自动登记
    /// </summary>
    Database = 4
}