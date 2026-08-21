// 定义所有接口统一返回格式以及分页结果格式。
using System.Diagnostics.CodeAnalysis;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     统一接口响应包装
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public sealed record ApiResponse<T>(int Code, string Msg, T? Data)
{
    /// <summary>
    ///     创建成功响应
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <param name="message">响应消息</param>
    /// <returns>成功响应对象</returns>
    // 创建成功响应，统一使用业务成功码。
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "工厂方法需保留现有统一响应调用方式。")]
    public static ApiResponse<T> Ok(
        T data
        , string message = "OK"
    ) {
        return new ApiResponse<T>(200, message, data);
    }
}