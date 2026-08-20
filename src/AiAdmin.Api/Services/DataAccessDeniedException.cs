// 定义数据权限范围不足时抛出的业务异常

namespace AiAdmin.Api.Services;

/// <summary>
///     表示当前用户无权操作目标数据
/// </summary>
public sealed class DataAccessDeniedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataAccessDeniedException" /> class.
    ///     初始化数据权限不足异常
    /// </summary>
    public DataAccessDeniedException()
        : base("Data scope does not allow this operation") {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataAccessDeniedException" /> class.
    ///     使用指定错误消息初始化数据权限不足异常
    /// </summary>
    /// <param name="message">描述异常原因的错误消息</param>
    public DataAccessDeniedException(string message)
        : base(message) {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataAccessDeniedException" /> class.
    ///     使用指定错误消息和内部异常初始化数据权限不足异常
    /// </summary>
    /// <param name="message">描述异常原因的错误消息</param>
    /// <param name="innerException">导致当前异常的内部异常</param>
    public DataAccessDeniedException(
        string message
        , Exception innerException
    )
        : base(message, innerException) {
    }
}