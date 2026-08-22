using AiAdmin.Api.Attributes;
using AiAdmin.Api.Data;

namespace AiAdmin.Api.Models;

/// <summary>
///     用户登录日志
/// </summary>
public sealed class LoginLog : EntityBase, IOwner
{
    /// <summary>
    ///     浏览器名称及版本
    /// </summary>
    [ListFilter("loginLog.fields.browser", Sort = 4)]
    public string Browser { get; init; } = string.Empty;

    /// <summary>
    ///     客户端 IP 地址
    /// </summary>
    [ListFilter("loginLog.fields.clientIp", Sort = 1)]
    public string ClientIp { get; init; } = string.Empty;

    /// <summary>
    ///     浏览器客户端提示原始 JSON
    /// </summary>
    public string ClientHints { get; init; } = string.Empty;

    /// <summary>
    ///     屏幕色深
    /// </summary>
    public int? ColorDepth { get; init; }

    /// <summary>
    ///     设备类型
    /// </summary>
    [ListFilter("loginLog.fields.deviceType", "select", Options = ["Desktop:loginLog.devices.desktop", "Mobile:loginLog.devices.mobile", "Tablet:loginLog.devices.tablet", "Unknown:loginLog.devices.unknown"], Sort = 5)]
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>
    ///     登录日志主键
    /// </summary>
    public long Id { get; init; } = SnowflakeIdGenerator.Next();

    /// <summary>
    ///     客户端语言
    /// </summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>
    ///     操作系统名称及版本
    /// </summary>
    [ListFilter("loginLog.fields.operatingSystem", Sort = 3)]
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>
    ///     所有者部门主键
    /// </summary>
    public long OwnerDepartmentId { get; set; }

    /// <summary>
    ///     所有者用户主键
    /// </summary>
    public long OwnerId { get; set; }

    /// <summary>
    ///     设备平台
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    ///     设备像素比
    /// </summary>
    public double? PixelRatio { get; init; }

    /// <summary>
    ///     IP 归属地区
    /// </summary>
    [ListFilter("loginLog.fields.region", Sort = 2)]
    public string Region { get; init; } = string.Empty;

    /// <summary>
    ///     屏幕分辨率
    /// </summary>
    public string ScreenResolution { get; init; } = string.Empty;

    /// <summary>
    ///     客户端时区
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    ///     最大触摸点数
    /// </summary>
    public int? TouchPoints { get; init; }

    /// <summary>
    ///     登录用户
    /// </summary>
    public User User { get; init; } = null!;

    /// <summary>
    ///     浏览器原始 User-Agent
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>
    ///     登录用户主键
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    ///     登录用户名快照
    /// </summary>
    [ListFilter("loginLog.fields.userName", Sort = 0)]
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    ///     浏览器视口尺寸
    /// </summary>
    public string ViewportSize { get; init; } = string.Empty;
}