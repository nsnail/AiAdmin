namespace AiAdmin.Api.Attributes;

/// <summary>
///     标记仅允许使用登录 JWT 访问的接口，API Key 不能调用，用于密码、资料和 API Key 管理等敏感操作
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class JwtOnlyAttribute : Attribute;