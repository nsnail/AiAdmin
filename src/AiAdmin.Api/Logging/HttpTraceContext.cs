using System.Globalization;

namespace AiAdmin.Api.Logging;

/// <summary>
///     在入口请求上下文与外部 HTTP 请求之间传递雪花链路主键
/// </summary>
public static class HttpTraceContext
{
    /// <summary>
    ///     外部 HTTP 请求携带链路主键的请求头名称
    /// </summary>
    public const string HEADER_NAME = "X-Trace-ID";

    private const string _ITEM_KEY = "AiAdmin.HttpTraceId";

    /// <summary>
    ///     将服务端生成的链路主键写入当前 HTTP 请求上下文
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="traceId">雪花链路主键</param>
    public static void Initialize(
        HttpContext context
        , long traceId
    ) {
        context.Items[_ITEM_KEY] = traceId;
        context.Request.Headers[HEADER_NAME] = traceId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     尝试从当前 HTTP 请求上下文读取链路主键
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="traceId">雪花链路主键</param>
    /// <returns>存在有效链路主键时返回 true</returns>
    public static bool TryGet(
        HttpContext? context
        , out long traceId
    ) {
        if (context?.Items.TryGetValue(_ITEM_KEY, out var value) == true && value is long currentTraceId) {
            traceId = currentTraceId;
            return true;
        }

        traceId = 0;
        return false;
    }
}