// 定义登录和当前用户信息相关的数据传输模型。

namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录页面配置
/// </summary>
public sealed record LoginConfigResult(bool LoginSliderVerification, bool RegistrationEnabled, bool EmailVerificationEnabled);