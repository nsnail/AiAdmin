// 定义动态筛选请求校验失败时使用的业务异常。

namespace AiAdmin.Api.Services;

/// <summary>
///     表示动态筛选请求格式不合法的异常
/// </summary>
public sealed class DynamicFilterValidationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicFilterValidationException" /> class
    ///     初始化动态筛选格式异常
    /// </summary>
    public DynamicFilterValidationException() {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicFilterValidationException" /> class
    ///     使用错误消息初始化动态筛选格式异常
    /// </summary>
    /// <param name="message">错误消息</param>
    public DynamicFilterValidationException(string message)
        : base(message) {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicFilterValidationException" /> class
    ///     使用错误消息和内部异常初始化动态筛选格式异常
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public DynamicFilterValidationException(
        string message
        , Exception innerException
    )
        : base(message, innerException) {
    }
}