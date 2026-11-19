using System.Net;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证请求日志的数值化转换：方法编号、路径 CRC32、IPv4 整数、耗时和业务码收窄
/// </summary>
public sealed class ApiRequestLogMapperTests
{
    /// <summary>
    ///     验证业务码超出 short 范围时返回空，范围内原样保留，空值保持为空
    /// </summary>
    [Fact]
    public void ToApiResponseCode_ReturnsNullWhenOutOfRange() {
        Assert.Equal((short)200, ApiRequestLogMapper.ToApiResponseCode(200));
        Assert.Equal((short)-1, ApiRequestLogMapper.ToApiResponseCode(-1));
        Assert.Null(ApiRequestLogMapper.ToApiResponseCode(100_000));
        Assert.Null(ApiRequestLogMapper.ToApiResponseCode(int.MinValue));
        Assert.Null(ApiRequestLogMapper.ToApiResponseCode(null));
    }

    /// <summary>
    ///     验证耗时超过 int 上限取上限，负数取 0
    /// </summary>
    [Fact]
    public void ToElapsedMilliseconds_ClampsToIntRange() {
        Assert.Equal(123, ApiRequestLogMapper.ToElapsedMilliseconds(123));
        Assert.Equal(int.MaxValue, ApiRequestLogMapper.ToElapsedMilliseconds(long.MaxValue));
        Assert.Equal(0, ApiRequestLogMapper.ToElapsedMilliseconds(-5));
    }

    /// <summary>
    ///     验证 IPv4 映射的 IPv6 地址按内嵌 IPv4 转换，真正的 IPv6 和空地址返回空
    /// </summary>
    [Fact]
    public void ToIpv4_HandlesMappedAndPureIpv6() {
        Assert.Equal(unchecked((int)0xC0A8010A), ApiRequestLogMapper.ToIpv4(IPAddress.Parse("::ffff:192.168.1.10")));
        Assert.Null(ApiRequestLogMapper.ToIpv4(IPAddress.Parse("2001:db8::1")));
        Assert.Null(ApiRequestLogMapper.ToIpv4(IPAddress.Parse("fe80::1")));
        Assert.Null(ApiRequestLogMapper.ToIpv4(IPAddress.IPv6Any));
        Assert.Null(ApiRequestLogMapper.ToIpv4(null));
    }

    /// <summary>
    ///     验证 IPv6 回环地址 ::1 按 127.0.0.1 记录，本机访问不会丢失来源，与 IPv4 回环得到相同的值
    /// </summary>
    [Fact]
    public void ToIpv4_MapsIpv6LoopbackToIpv4Loopback() {
        Assert.Equal(0x7F000001, ApiRequestLogMapper.ToIpv4(IPAddress.IPv6Loopback));
        Assert.Equal(ApiRequestLogMapper.ToIpv4(IPAddress.Loopback), ApiRequestLogMapper.ToIpv4(IPAddress.IPv6Loopback));
        Assert.Equal(ApiRequestLogMapper.ToIpv4(IPAddress.Loopback), ApiRequestLogMapper.ToIpv4(IPAddress.Parse("::ffff:127.0.0.1")));
    }

    /// <summary>
    ///     验证转换结果能还原为点分十进制文本
    /// </summary>
    [Fact]
    public void ToIpv4_RoundTripsToDottedText() {
        var value = ApiRequestLogMapper.ToIpv4(IPAddress.Parse("172.16.5.104"))!.Value;

        var bytes = new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        Assert.Equal("172.16.5.104", new IPAddress(bytes).ToString());
    }

    /// <summary>
    ///     验证 IPv4 按网络字节序转换，首段位于最高字节
    /// </summary>
    /// <param name="ip">IP 文本</param>
    /// <param name="expected">期望的 32 位整数</param>
    [Theory]
    [InlineData("127.0.0.1", 0x7F000001)]
    [InlineData("192.168.1.10", unchecked((int)0xC0A8010A))]
    [InlineData("10.0.0.5", 0x0A000005)]
    [InlineData("0.0.0.0", 0)]
    [InlineData("255.255.255.255", -1)]
    public void ToIpv4_UsesNetworkByteOrder(
        string ip
        , int expected
    ) {
        Assert.Equal(expected, ApiRequestLogMapper.ToIpv4(IPAddress.Parse(ip)));
    }

    /// <summary>
    ///     验证方法名不区分大小写地转换为编号，未知方法和空值记为 Other
    /// </summary>
    /// <param name="method">HTTP 方法名</param>
    /// <param name="expected">期望的方法编号</param>
    [Theory]
    [InlineData("GET", ApiRequestMethod.Get)]
    [InlineData("post", ApiRequestMethod.Post)]
    [InlineData("Put", ApiRequestMethod.Put)]
    [InlineData("DELETE", ApiRequestMethod.Delete)]
    [InlineData("PATCH", ApiRequestMethod.Patch)]
    [InlineData("HEAD", ApiRequestMethod.Head)]
    [InlineData("OPTIONS", ApiRequestMethod.Options)]
    [InlineData("TRACE", ApiRequestMethod.Other)]
    [InlineData("", ApiRequestMethod.Other)]
    [InlineData(null, ApiRequestMethod.Other)]
    [InlineData("99", ApiRequestMethod.Other)]
    public void ToMethod_MapsKnownMethods(
        string? method
        , ApiRequestMethod expected
    ) {
        Assert.Equal(expected, ApiRequestLogMapper.ToMethod(method));
    }

    /// <summary>
    ///     验证路径 CRC32 忽略查询字符串、大小写和首尾斜杠，与接口表的计算规则一致
    /// </summary>
    [Fact]
    public void ToPathCrc32_IgnoresQueryAndCase() {
        var expected = ApiEndpointKey.ComputePathCrc32("/api/user/list");

        Assert.Equal(expected, ApiRequestLogMapper.ToPathCrc32("/api/user/list?a=1&b=2"));
        Assert.Equal(expected, ApiRequestLogMapper.ToPathCrc32("/API/User/List/"));
        Assert.Equal(expected, ApiRequestLogMapper.ToPathCrc32("api/user/list"));
        Assert.NotEqual(expected, ApiRequestLogMapper.ToPathCrc32("/api/user/info"));
    }
}