namespace AiAdmin.Api.Contracts;

/// <summary>
///     统一接口响应包装
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
/// <param name="Code">响应状态码</param>
/// <param name="Msg">响应消息</param>
/// <param name="Data">响应数据</param>
public sealed record ApiResponse<T>(int Code, string Msg, T? Data)
{
    /// <summary>
    ///     创建成功响应
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <param name="message">响应消息</param>
    /// <returns>成功响应对象</returns>
    public static ApiResponse<T> Ok(
        T data
        , string message = "OK"
    ) {
        return new ApiResponse<T>(200, message, data);
    }
}