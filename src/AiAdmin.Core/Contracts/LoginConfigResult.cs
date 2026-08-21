namespace AiAdmin.Api.Contracts;

/// <summary>
///     登录页面配置
/// </summary>
public sealed record LoginConfigResult(bool LoginSliderVerification, bool RegistrationEnabled, bool EmailVerificationEnabled);