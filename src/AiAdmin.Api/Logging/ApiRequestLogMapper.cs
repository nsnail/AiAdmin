using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;

namespace AiAdmin.Api.Logging;

/// <summary>
///     把请求的原始信息转换为请求日志中占用空间最小的数值表示：方法编号、路径 CRC32、IPv4 整数以及收窄后的耗时和状态码
/// </summary>
public static class ApiRequestLogMapper
{
    /// <summary>
    ///     把 HTTP 方法名转换为方法编号，不区分大小写，无法识别的方法记为 Other
    /// </summary>
    /// <param name="method">HTTP 方法名</param>
    /// <returns>方法编号</returns>
    public static ApiRequestMethod ToMethod(string? method) {
        return Enum.TryParse<ApiRequestMethod>(method, true, out var result) && Enum.IsDefined(result) ? result : ApiRequestMethod.Other;
    }

    /// <summary>
    ///     计算不含查询字符串的规范化路径的 CRC32，与接口表使用同一规则
    /// </summary>
    /// <param name="path">请求路径，可带查询字符串，会被忽略</param>
    /// <returns>有符号 CRC32</returns>
    public static int ToPathCrc32(string path) {
        return ApiEndpointKey.ComputePathCrc32(path);
    }

    /// <summary>
    ///     把 IP 地址转换为 IPv4 的 32 位整数，IPv4 映射的 IPv6 地址（::ffff:a.b.c.d）按其内嵌的 IPv4 处理，IPv6 回环地址（::1）按 127.0.0.1 处理，
    ///     其余真正的 IPv6 返回空
    /// </summary>
    /// <param name="address">IP 地址</param>
    /// <returns>32 位整数，高位字节对应地址首段，无法表示时返回 null</returns>
    public static int? ToIpv4(IPAddress? address) {
        if (address is null) {
            return null;
        }

        // 双栈监听（http://[::]:port）下本机访问 localhost 走 IPv6，来源和本机地址都是 ::1，等价于 127.0.0.1
        if (IPAddress.IPv6Loopback.Equals(address)) {
            return 0x7F000001;
        }

        if (address.IsIPv4MappedToIPv6) {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork) {
            return null;
        }

        // 网络字节序（大端）读取，保证 a.b.c.d 对应 a 在最高字节
        return BinaryPrimitives.ReadInt32BigEndian(address.GetAddressBytes());
    }

    /// <summary>
    ///     把毫秒数收窄为 int，超过上限时取上限，负数按 0 处理
    /// </summary>
    /// <param name="milliseconds">毫秒数</param>
    /// <returns>收窄后的毫秒数</returns>
    public static int ToElapsedMilliseconds(long milliseconds) {
        return (int)Math.Clamp(milliseconds, 0, int.MaxValue);
    }

    /// <summary>
    ///     把业务码收窄为 short，超出范围时返回空
    /// </summary>
    /// <param name="code">业务码</param>
    /// <returns>收窄后的业务码</returns>
    public static short? ToApiResponseCode(int? code) {
        return code is >= short.MinValue and <= short.MaxValue ? (short)code.Value : null;
    }
}