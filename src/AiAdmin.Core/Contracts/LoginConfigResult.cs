namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录页面配置
/// </summary>
/// <param name="LoginSliderVerification">是否启用登录滑块验证</param>
/// <param name="RegistrationEnabled">是否允许注册</param>
/// <param name="EmailVerificationEnabled">是否启用邮箱验证</param>
public sealed record LoginConfigResult(bool LoginSliderVerification, bool RegistrationEnabled, bool EmailVerificationEnabled);