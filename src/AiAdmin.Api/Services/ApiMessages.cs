// 提供接口业务消息和接口描述的中英文转换。

namespace AiAdmin.Api.Services;

/// <summary>
///     提供业务消息和接口描述的多语言转换
/// </summary>
public static class ApiMessages
{
    private static readonly Dictionary<string, string> _apiDescriptions = new()
    {
        ["Authentication"] = "身份认证"
        , ["Sign in to the system"] = "登录系统"
        , ["User management"] = "用户管理"
        , ["Create user"] = "新增用户"
        , ["Delete user"] = "删除用户"
        , ["Get current user information"] = "获取当前用户信息"
        , ["Query user list"] = "查询用户列表"
        , ["Query assignable roles"] = "查询可分配角色"
        , ["Update user"] = "修改用户"
        , ["Role management"] = "角色管理"
        , ["Create role"] = "新增角色"
        , ["Delete role"] = "删除角色"
        , ["Query role list"] = "查询角色列表"
        , ["Query role menu permissions"] = "查询角色菜单权限"
        , ["Query role API permissions"] = "查询角色接口权限"
        , ["Save role menu permissions"] = "保存角色菜单权限"
        , ["Save role API permissions"] = "保存角色接口权限"
        , ["Update role"] = "修改角色"
        , ["Menu management"] = "菜单管理"
        , ["Create menu"] = "新增菜单"
        , ["Get current user menus"] = "获取当前用户菜单"
        , ["Delete menu"] = "删除菜单"
        , ["Query menu list"] = "查询菜单列表"
        , ["Update menu"] = "修改菜单"
        , ["API management"] = "接口管理"
        , ["Query API list"] = "查询接口列表"
        , ["Synchronize system APIs"] = "同步系统接口"
        , ["Update API anonymous access"] = "修改接口匿名访问"
    };

    private static readonly Dictionary<string, (string Zh, string En)> _messages = new()
    {
        ["ok"] = ("操作成功", "Operation succeeded")
        , ["loginSuccess"] = ("登录成功", "Login successful")
        , ["invalidCredentials"] = ("用户名或密码错误", "Invalid username or password")
        , ["userExists"] = ("用户名已存在", "Username already exists")
        , ["passwordRequired"] = ("新增用户时密码必填", "Password is required for a new user")
        , ["invalidRole"] = ("包含无效角色", "One or more roles are invalid")
        , ["userCreated"] = ("用户创建成功", "User created")
        , ["userNotFound"] = ("用户不存在", "User not found")
        , ["userUpdated"] = ("用户更新成功", "User updated")
        , ["cannotDeleteSelf"] = ("不能删除当前登录用户", "You cannot delete your own account")
        , ["userDeleted"] = ("用户删除成功", "User deleted")
        , ["roleSuper"] = ("超级管理员", "Super administrator")
        , ["roleAdmin"] = ("管理员", "Administrator")
        , ["roleUser"] = ("普通用户", "User")
    };

    /// <summary>
    ///     按请求语言获取业务消息
    /// </summary>
    /// <param name="request">当前 HTTP 请求</param>
    /// <param name="key">消息键</param>
    /// <returns>本地化消息</returns>
    public static string Get(
        HttpRequest request
        , string key
    ) {
        // 根据 Accept-Language 选择中文或英文业务消息。
        var (zh, en) = _messages[key];
        return request.Headers.AcceptLanguage.ToString().StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zh : en;
    }

    /// <summary>
    ///     按请求语言获取接口描述
    /// </summary>
    /// <param name="request">当前 HTTP 请求</param>
    /// <param name="englishDescription">英文描述键</param>
    /// <returns>本地化接口描述</returns>
    public static string GetApiDescription(
        HttpRequest request
        , string englishDescription
    ) {
        // 接口表始终保存英文键，展示时按请求语言转换为中文。
        if (!request.Headers.AcceptLanguage.ToString().StartsWith("zh", StringComparison.OrdinalIgnoreCase)) {
            // 接口表始终保存英文键，展示时按请求语言转换为中文。
            return englishDescription;
        }

        return _apiDescriptions.TryGetValue(englishDescription, out var chineseDescription)
            ?

            // 接口表始终保存英文键，展示时按请求语言转换为中文。
            chineseDescription
            :

            // 接口表始终保存英文键，展示时按请求语言转换为中文。
            englishDescription;
    }
}