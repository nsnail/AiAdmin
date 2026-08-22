namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录时浏览器采集的客户端信息
/// </summary>
public sealed class LoginClientInfo
{
    /// <summary>
    ///     浏览器名称及版本
    /// </summary>
    public string? Browser { get; init; }

    /// <summary>
    ///     浏览器客户端提示原始 JSON
    /// </summary>
    public string? ClientHints { get; init; }

    /// <summary>
    ///     屏幕色深
    /// </summary>
    public int? ColorDepth { get; init; }

    /// <summary>
    ///     设备类型
    /// </summary>
    public string? DeviceType { get; init; }

    /// <summary>
    ///     客户端语言
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    ///     操作系统名称及版本
    /// </summary>
    public string? OperatingSystem { get; init; }

    /// <summary>
    ///     设备平台
    /// </summary>
    public string? Platform { get; init; }

    /// <summary>
    ///     设备像素比
    /// </summary>
    public double? PixelRatio { get; init; }

    /// <summary>
    ///     屏幕分辨率
    /// </summary>
    public string? ScreenResolution { get; init; }

    /// <summary>
    ///     客户端时区
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    ///     最大触摸点数
    /// </summary>
    public int? TouchPoints { get; init; }

    /// <summary>
    ///     浏览器视口尺寸
    /// </summary>
    public string? ViewportSize { get; init; }
}

// End of client information contract