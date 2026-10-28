namespace AiAdmin.Api.Models;

/// <summary>
///     API 请求日志中记录的 HTTP 方法编号，以一个字节存储，未知方法记为 Other
/// </summary>
public enum ApiRequestMethod : byte
{
    /// <summary>
    ///     其他或无法识别的方法
    /// </summary>
    Other = 0

    ,

    /// <summary>
    ///     GET
    /// </summary>
    Get = 1

    ,

    /// <summary>
    ///     POST
    /// </summary>
    Post = 2

    ,

    /// <summary>
    ///     PUT
    /// </summary>
    Put = 3

    ,

    /// <summary>
    ///     DELETE
    /// </summary>
    Delete = 4

    ,

    /// <summary>
    ///     PATCH
    /// </summary>
    Patch = 5

    ,

    /// <summary>
    ///     HEAD
    /// </summary>
    Head = 6

    ,

    /// <summary>
    ///     OPTIONS
    /// </summary>
    Options = 7
}